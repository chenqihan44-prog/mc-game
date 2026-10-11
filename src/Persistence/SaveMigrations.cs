using System.IO;
using EmberLighthouse.World;

namespace EmberLighthouse.Persistence;

public sealed class UnsupportedSaveException(string message) : IOException(message);
public static class SaveMigrations
{
    public static bool ToCurrent(WorldSnapshot snapshot)
    {
        if(snapshot.GeneratorVersion!=Terrain.Version)throw new UnsupportedSaveException("不支持这个生成器版本；原世界已保留，请使用对应版本");
        if(snapshot.FormatVersion<1||snapshot.FormatVersion>WorldSnapshot.CurrentFormat)throw new UnsupportedSaveException("不支持这个存档格式；原世界已保留，请使用对应版本");
        bool migrated=snapshot.FormatVersion!=WorldSnapshot.CurrentFormat;
        while(snapshot.FormatVersion<WorldSnapshot.CurrentFormat){
            switch(snapshot.FormatVersion){case 1: snapshot.FormatVersion=2;break;case 2:snapshot.FormatVersion=3;break;case 3:snapshot.Equipment=new(5);snapshot.FormatVersion=4;break;default:throw new UnsupportedSaveException("缺少存档迁移步骤");}
        }
        return migrated;
    }
}
