using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using EmberLighthouse.Core;
using EmberLighthouse.Items;
using EmberLighthouse.World;

namespace EmberLighthouse.Persistence;

public sealed record EncodedChunk(byte[] Bytes,string Encoding,int ModifiedCount);
public static class ChunkCodec
{
    public const int DenseThreshold=2048;
    private const int Magic=0x48434C45,MaxRawBytes=262144;
    private static readonly UTF8Encoding Utf8=new(false,true);
    public static EncodedChunk Encode(Cell chunk,IReadOnlyDictionary<Cell,string> changes,Terrain terrain,Catalog catalog)
    {
        CheckChunk(chunk);var edits=new SortedDictionary<int,string>();
        foreach(var entry in changes){
            if(!Coordinates.InWorld(entry.Key)||Coordinates.Chunk(entry.Key)!=chunk)throw new InvalidDataException("区块修改坐标无效");
            ushort value=catalog.Number(entry.Value);if(terrain.Get(entry.Key)!=value)edits.Add(Coordinates.Index(entry.Key),entry.Value);
        }
        bool dense=edits.Count>=DenseThreshold;string[] values;
        if(dense){values=terrain.Generate(chunk).Select(catalog.Id).ToArray();foreach(var edit in edits)values[edit.Key]=edit.Value;}
        else values=edits.Values.ToArray();
        string[] palette=values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var numbers=palette.Select((id,index)=>(id,index)).ToDictionary(p=>p.id,p=>(ushort)p.index,StringComparer.Ordinal);
        using var raw=new MemoryStream();using(var writer=new BinaryWriter(raw,Utf8,true)){
            writer.Write(Magic);writer.Write((byte)1);writer.Write(dense);writer.Write(chunk.X);writer.Write(chunk.Y);writer.Write(chunk.Z);writer.Write(terrain.Seed);writer.Write(Terrain.Version);writer.Write((ushort)edits.Count);writer.Write((ushort)palette.Length);
            foreach(string id in palette){byte[] bytes=Utf8.GetBytes(id);if(bytes.Length is <1 or >128)throw new InvalidDataException("方块 ID 长度无效");writer.Write((byte)bytes.Length);writer.Write(bytes);}
            writer.Write((ushort)values.Length);
            if(dense)foreach(string id in values)writer.Write(numbers[id]);
            else foreach(var edit in edits){writer.Write((ushort)edit.Key);writer.Write(numbers[edit.Value]);}
        }
        using var compressed=new MemoryStream();using(var gzip=new GZipStream(compressed,CompressionLevel.Fastest,true))gzip.Write(raw.GetBuffer(),0,(int)raw.Length);
        return new(compressed.ToArray(),dense?"dense":"sparse",edits.Count);
    }
    public static Dictionary<Cell,string> Decode(byte[] compressed,Cell chunk,Terrain terrain,Catalog catalog,string expectedEncoding,int expectedChanges)
    {
        CheckChunk(chunk);if(compressed.Length>MaxRawBytes)throw new InvalidDataException("压缩区块过大");
        using var input=new MemoryStream(compressed,false);using var gzip=new GZipStream(input,CompressionMode.Decompress);using var raw=new MemoryStream();
        var buffer=new byte[4096];int read;
        while((read=gzip.Read(buffer,0,buffer.Length))>0){if(raw.Length+read>MaxRawBytes)throw new InvalidDataException("解压区块过大");raw.Write(buffer,0,read);}
        raw.Position=0;using var reader=new BinaryReader(raw,Utf8,true);
        if(reader.ReadInt32()!=Magic||reader.ReadByte()!=1)throw new InvalidDataException("区块格式不受支持");
        bool dense=reader.ReadBoolean();if(new Cell(reader.ReadInt32(),reader.ReadInt32(),reader.ReadInt32())!=chunk||reader.ReadInt32()!=terrain.Seed||reader.ReadInt32()!=Terrain.Version)throw new InvalidDataException("区块归属或生成器版本不符");
        int modifications=reader.ReadUInt16(),paletteCount=reader.ReadUInt16();
        if(modifications!=expectedChanges||modifications>8192||(dense?"dense":"sparse")!=expectedEncoding||paletteCount>catalog.BlockIds.Length)throw new InvalidDataException("区块清单不符");
        var palette=new string[paletteCount];var known=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<paletteCount;i++){int length=reader.ReadByte();if(length is <1 or >128)throw new InvalidDataException("区块方块 ID 长度无效");byte[] bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();string id=Utf8.GetString(bytes);if(!catalog.BlockIds.Contains(id,StringComparer.Ordinal)||!known.Add(id))throw new InvalidDataException("区块包含未知或重复方块 ID");palette[i]=id;}
        int count=reader.ReadUInt16();if(count!=(dense?8192:modifications))throw new InvalidDataException("区块数据数量无效");
        var result=new Dictionary<Cell,string>();var origin=Coordinates.Origin(chunk);ushort[]? generated=dense?terrain.Generate(chunk):null;int previous=-1;
        for(int i=0;i<count;i++){
            int index=dense?i:reader.ReadUInt16(),number=reader.ReadUInt16();if(index<=previous||index>=8192||number>=palette.Length)throw new InvalidDataException("区块索引无效");previous=index;
            var cell=origin+new Cell(index%16,index/256,index/16%16);string id=palette[number];ushort original=dense?generated![index]:terrain.Get(cell);
            if(catalog.Number(id)!=original)result.Add(cell,id);
        }
        if(result.Count!=modifications||raw.Position!=raw.Length)throw new InvalidDataException("区块修改数量或结尾无效");return result;
    }
    private static void CheckChunk(Cell chunk)
    {
        if(chunk.X< -128||chunk.X>=128||chunk.Z< -128||chunk.Z>=128||chunk.Y<0||chunk.Y>=6)throw new InvalidDataException("区块坐标越界");
    }
}
