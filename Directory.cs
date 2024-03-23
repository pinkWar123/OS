using System;
using System.IO;
using System.Text;
class FATDirectory : FATFileManager
{
    public List<FileManager> Children;
    public FATDirectory() { }

    public override void CloneData(byte[] data)
    {
        base.CloneData(data);
        IsDelete = false;
        IsFile = false;
        IsFAT32 = true;
        Children = new List<FileManager>();
    }

    public override void PrintImfomations(int level)
    {
        for (int i = 0; i < level; i++)
        {
            Console.Write("\t");
        }
        Console.WriteLine(MainName + "--" + FileSize + "--" + Creationdatetime.Day + "/" + Creationdatetime.Month + "/" + Creationdatetime.Year + "-" + Creationdatetime.Hour + ":" + Creationdatetime.Minute + ":" + Creationdatetime.Second);
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].PrintImfomations(level + 1);
        }
    }
}