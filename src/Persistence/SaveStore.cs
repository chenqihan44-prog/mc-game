using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.World;
namespace EmberLighthouse.Persistence;

public sealed record ChunkReference(string Hash,string Encoding,int ModifiedCount);
public sealed record SaveManifest(int Version,string Generation,string Hash,DateTime SavedUtc,Dictionary<string,ChunkReference>? Chunks=null);
public sealed record LoadedWorld(WorldSnapshot Snapshot,bool Recovered,string Generation,bool Migrated=false);
public sealed record BackupEntry(string Key,string Name,DateTime SavedUtc,bool Damaged);

public sealed class SaveStore(string root,Catalog catalog)
{
    public string Root {get;}=root;
    private readonly object saveGate=new();
    private static readonly JsonSerializerOptions MetadataOptions=CreateMetadataOptions();
    private static JsonSerializerOptions CreateMetadataOptions()
    {
        var resolver=new DefaultJsonTypeInfoResolver();resolver.Modifiers.Add(info=>{if(info.Type==typeof(WorldSnapshot))info.Properties.Remove(info.Properties.First(p=>p.Name=="Changes"));});
        return new(Catalog.JsonOptions){WriteIndented=false,TypeInfoResolver=resolver};
    }
    public string Save(WorldSnapshot input,Action<string>? fault=null)
    {
        var snapshot=input.Copy();SaveMigrations.ToCurrent(snapshot);_ = new WorldState(catalog,snapshot);
        lock(saveGate){
            string path=DirectoryFor(snapshot.WorldId);Directory.CreateDirectory(path);
            using var transaction=new FileStream(Path.Combine(path,".saving.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
            ReadPointer(path);var previous=ValidGenerations(path,true).FirstOrDefault();
            if(previous==null&&Directory.GetFiles(path,"*.commit").Length>0)throw new InvalidDataException("原世界没有完整保存，拒绝覆盖；请先从备份恢复");
            if(previous!=null&&(previous.Version==1||ReadMetadata(path,previous).FormatVersion<WorldSnapshot.CurrentFormat))CopyBackup(path,previous,snapshot.WorldId);
            string generation=DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff")+"-"+Guid.NewGuid().ToString("N")[..8];
            var chunks=new Dictionary<string,ChunkReference>();var terrain=new Terrain(snapshot.Seed,catalog);Directory.CreateDirectory(Path.Combine(path,"chunks"));
            foreach(var group in snapshot.Changes.Select(p=>(Cell:Cell.Parse(p.Key),Id:p.Value)).GroupBy(p=>Coordinates.Chunk(p.Cell)).OrderBy(p=>p.Key.ToString(),StringComparer.Ordinal)){
                var encoded=ChunkCodec.Encode(group.Key,group.ToDictionary(p=>p.Cell,p=>p.Id),terrain,catalog);if(encoded.ModifiedCount==0)continue;
                string hash=Convert.ToHexString(SHA256.HashData(encoded.Bytes));string file=ChunkFile(path,hash);
                if(File.Exists(file)){if(!HashMatches(file,hash))throw new InvalidDataException("已有区块文件校验失败，原档已保留");}
                else{WriteFlushed(file+".tmp",encoded.Bytes);fault?.Invoke("chunk-written");File.Move(file+".tmp",file);fault?.Invoke("chunk-committed");}
                chunks.Add(group.Key.ToString(),new(hash,encoded.Encoding,encoded.ModifiedCount));
            }
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(snapshot,MetadataOptions);string stateFile=Path.Combine(path,generation+".json");
            WriteFlushed(stateFile+".tmp",bytes);fault?.Invoke("state-written");File.Move(stateFile+".tmp",stateFile);
            var manifest=new SaveManifest(2,generation,Convert.ToHexString(SHA256.HashData(bytes)),DateTime.UtcNow,chunks);
            byte[] commit=JsonSerializer.SerializeToUtf8Bytes(manifest,Catalog.JsonOptions);string manifestFile=Path.Combine(path,generation+".commit");
            WriteFlushed(manifestFile+".tmp",commit);fault?.Invoke("manifest-written");File.Move(manifestFile+".tmp",manifestFile);fault?.Invoke("generation-committed");
            WriteFlushed(Path.Combine(path,"CURRENT.tmp"),commit);fault?.Invoke("pointer-written");File.Move(Path.Combine(path,"CURRENT.tmp"),Path.Combine(path,"CURRENT"),true);fault?.Invoke("pointer-committed");
            Prune(path);return generation;
        }
    }
    public LoadedWorld Load(string id)
    {
        string path=DirectoryFor(id);if(!Directory.Exists(path))throw new FileNotFoundException("世界不存在");var pointer=ReadPointer(path);
        foreach(var manifest in ValidGenerations(path)){
            try{var loaded=ReadGeneration(path,manifest);if(loaded.Snapshot.WorldId!=id)continue;return new(loaded.Snapshot,pointer?.Generation!=manifest.Generation||pointer.Hash!=manifest.Hash,manifest.Generation,loaded.Migrated);}
            catch(Exception e)when(IsDamaged(e)){}
        }
        throw new InvalidDataException("没有完整可恢复的存档，文件已保留，请从备份恢复");
    }
    private (WorldSnapshot Snapshot,bool Migrated) ReadGeneration(string path,SaveManifest manifest)
    {
        var state=ReadMetadata(path,manifest);bool migrated=SaveMigrations.ToCurrent(state);
        if(manifest.Version==2){
            if(state.Changes.Count!=0)throw new InvalidDataException("玩家快照混入区块修改");var terrain=new Terrain(state.Seed,catalog);
            foreach(var pair in manifest.Chunks!){var reference=pair.Value;var changes=ChunkCodec.Decode(File.ReadAllBytes(ChunkFile(path,reference.Hash)),Cell.Parse(pair.Key),terrain,catalog,reference.Encoding,reference.ModifiedCount);foreach(var edit in changes)state.Changes.Add(edit.Key.ToString(),edit.Value);}
        }
        _ = new WorldState(catalog,state);return(state,migrated);
    }
    public IEnumerable<(string Id,string Name,bool Damaged)> List()
    {
        if(!Directory.Exists(Root))yield break;
        foreach(string path in Directory.GetDirectories(Root)){string id=Path.GetFileName(path);WorldSnapshot? snapshot=null;try{snapshot=Load(id).Snapshot;}catch(Exception e)when(IsDamaged(e)||e is UnsupportedSaveException){}yield return(id,snapshot?.Name??id,snapshot==null);}
    }
    public string Backup(string id)
    {
        lock(saveGate){string path=DirectoryFor(id);using var transaction=new FileStream(Path.Combine(path,".saving.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);var loaded=Load(id);var manifest=ValidGenerations(path).First(m=>m.Generation==loaded.Generation);return CopyBackup(path,manifest,id);}
    }
    private string CopyBackup(string path,SaveManifest manifest,string id)
    {
        string destination=Path.Combine(BackupRoot,id+"-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]);Directory.CreateDirectory(destination);
        foreach(string suffix in new[]{".json",".commit"})File.Copy(Path.Combine(path,manifest.Generation+suffix),Path.Combine(destination,manifest.Generation+suffix));
        if(manifest.Chunks!=null){Directory.CreateDirectory(Path.Combine(destination,"chunks"));foreach(string hash in manifest.Chunks.Values.Select(c=>c.Hash).Distinct())File.Copy(ChunkFile(path,hash),ChunkFile(destination,hash));}
        File.Copy(Path.Combine(path,manifest.Generation+".commit"),Path.Combine(destination,"CURRENT"));return Path.GetFullPath(destination);
    }
    private string BackupRoot=>Path.GetFullPath(Path.Combine(Root,"..","backups"));
    public IEnumerable<BackupEntry> ListBackups()
    {
        if(!Directory.Exists(BackupRoot))yield break;
        foreach(string path in Directory.GetDirectories(BackupRoot).OrderByDescending(Path.GetFileName,StringComparer.Ordinal)){
            string key=Path.GetFileName(path);WorldSnapshot? snapshot=null;DateTime date=Directory.GetCreationTimeUtc(path);
            try{var read=ReadBackup(key);snapshot=read.Snapshot;date=read.SavedUtc;}catch(Exception e)when(IsDamaged(e)||e is UnsupportedSaveException){}yield return new(key,snapshot?.Name??key,date,snapshot==null);
        }
    }
    public string RestoreBackup(string key)
    {
        var snapshot=ReadBackup(key).Snapshot;snapshot.WorldId=Guid.NewGuid().ToString("N");snapshot.Name+="（恢复备份）";Save(snapshot);return snapshot.WorldId;
    }
    private (WorldSnapshot Snapshot,DateTime SavedUtc) ReadBackup(string key)
    {
        if(string.IsNullOrWhiteSpace(key)||key!=Path.GetFileName(key)||key.IndexOfAny(Path.GetInvalidFileNameChars())>=0||key.Contains(".."))throw new ArgumentException("无效的备份名称");
        string path=Path.GetFullPath(Path.Combine(BackupRoot,key));if(!path.StartsWith(BackupRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("备份路径越界");
        if(!Directory.Exists(path))throw new DirectoryNotFoundException("备份不存在");if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("不支持链接形式的备份目录");ReadPointer(path);
        foreach(var manifest in ValidGenerations(path)){try{var snapshot=ReadGeneration(path,manifest).Snapshot;DirectoryFor(snapshot.WorldId);return(snapshot,manifest.SavedUtc);}catch(Exception e)when(IsDamaged(e)){} }
        throw new InvalidDataException("备份没有完整且校验通过的保存代际");
    }
    private static SaveManifest? ReadPointer(string path)
    {
        try{var pointer=JsonSerializer.Deserialize<SaveManifest>(File.ReadAllBytes(Path.Combine(path,"CURRENT")),Catalog.JsonOptions);if(pointer?.Version>2)throw new UnsupportedSaveException("存档提交格式来自较新版本，拒绝读取或覆盖");return pointer;}catch(Exception e)when(IsDamaged(e)){return null;}
    }
    private IEnumerable<SaveManifest> ValidGenerations(string path,bool validatePayload=false)
    {
        foreach(string file in Directory.GetFiles(path,"*.commit").OrderByDescending(Path.GetFileName,StringComparer.Ordinal)){
            SaveManifest? manifest=null;
            try{
                manifest=JsonSerializer.Deserialize<SaveManifest>(File.ReadAllBytes(file),Catalog.JsonOptions);if(manifest==null||Path.GetFileNameWithoutExtension(file)!=manifest.Generation||!IsHash(manifest.Hash)||!HashMatches(Path.Combine(path,manifest.Generation+".json"),manifest.Hash))continue;
                if(manifest.Version>2)throw new UnsupportedSaveException("存档提交格式来自较新版本，拒绝读取或覆盖");if(manifest.Version<1)continue;
                var metadata=ReadMetadata(path,manifest);SaveMigrations.ToCurrent(metadata);_ = new WorldState(catalog,metadata);
                if(manifest.Version==2&&(manifest.Chunks==null||manifest.Chunks.Any(p=>p.Value==null||p.Value.ModifiedCount<1||p.Value.ModifiedCount>8192||p.Value.Encoding is not("sparse" or "dense")||!IsHash(p.Value.Hash)||!HashMatches(ChunkFile(path,p.Value.Hash),p.Value.Hash))))continue;
                if(validatePayload)ReadGeneration(path,manifest);
            }catch(Exception e)when(IsDamaged(e)){continue;}
            yield return manifest;
        }
    }
    private static WorldSnapshot ReadMetadata(string path,SaveManifest manifest)=>JsonSerializer.Deserialize<WorldSnapshot>(File.ReadAllBytes(Path.Combine(path,manifest.Generation+".json")),Catalog.JsonOptions)??throw new InvalidDataException("玩家快照为空");
    private void Prune(string path)
    {
        var valid=ValidGenerations(path,true).ToArray();foreach(var old in valid.Skip(3)){File.Delete(Path.Combine(path,old.Generation+".commit"));File.Delete(Path.Combine(path,old.Generation+".json"));}
        var referenced=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string file in Directory.GetFiles(path,"*.commit")){try{var m=JsonSerializer.Deserialize<SaveManifest>(File.ReadAllBytes(file),Catalog.JsonOptions);if(m?.Chunks!=null)foreach(var c in m.Chunks.Values)if(c!=null&&IsHash(c.Hash))referenced.Add(c.Hash);}catch(JsonException){} }
        foreach(string file in Directory.GetFiles(Path.Combine(path,"chunks"),"*.chunk")){string hash=Path.GetFileNameWithoutExtension(file);if(IsHash(hash)&&!referenced.Contains(hash)&&File.GetLastWriteTimeUtc(file)<DateTime.UtcNow.AddMinutes(-2))File.Delete(file);}
    }
    private static bool IsDamaged(Exception e)=>e is not UnsupportedSaveException&&(e is IOException or InvalidDataException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException);
    private static bool IsHash(string value)=>value!=null&&value.Length==64&&value.All(Uri.IsHexDigit);
    private static bool HashMatches(string file,string expected){using var stream=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(stream)).Equals(expected,StringComparison.OrdinalIgnoreCase);}
    private static string ChunkFile(string path,string hash){if(!IsHash(hash))throw new InvalidDataException("区块哈希无效");return Path.Combine(path,"chunks",hash.ToUpperInvariant()+".chunk");}
    private string DirectoryFor(string id){if(id==null||id.Length!=32||!id.All(Uri.IsHexDigit))throw new ArgumentException("Invalid world ID");return Path.Combine(Root,id);}
    private static void WriteFlushed(string path,byte[] bytes){using var stream=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough);stream.Write(bytes);stream.Flush(true);}
}
