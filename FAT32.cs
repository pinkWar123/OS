using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Collections.Generic;
class FAT32
{
    // This area is used for Boost_Sector's Variables
    public UInt16 BytesPerSector;
    public byte SectorPerCluster;
    public UInt16 ReversedSector;
    public byte NumberOfFAT;
    public UInt32 VolumeSize;
    public UInt32 SectorPerFAT;
    public UInt32 StartingClusterOfRDET;
    public string FATType;


    //--------------------------------------------------------------

    // This area is used for FAT's Variables

    UInt32[] FATTable;
    //--------------------------------------------------------------

    
    //--------------------------------------------------------------

    public FAT32()
    {
        FATType = ""; // t de vay de no khong bao loi nua
    }
    public FAT32(FileStream fileStream)
    {

    }
    public FAT32(string file)
    {
        using (FileStream filestream = new FileStream(file,FileMode.Open, FileAccess.Read))
        {
            ReadBoostSector(filestream);
            ReadFAT1(filestream);
            
        }
    }
    ~FAT32() { }

    public List<FileManager> ReadFiles(string file)
    {
        using (FileStream filestream = new FileStream(file, FileMode.Open, FileAccess.Read))
        {
            ReadBoostSector(filestream);
            ReadFAT1(filestream);

            List<FileManager> files = new List<FileManager>();
            ReadDET(filestream, StartingClusterOfRDET, ref files);
            return files;
        }
    }
    public void ReadBoostSector(FileStream fileStream)
    {
        fileStream.Seek(0, SeekOrigin.Begin);

        // Only read main methods in Boost Sector---From teacher
        byte[] bootSectorBytes = new byte[512];
        fileStream.Read(bootSectorBytes, 0, bootSectorBytes.Length);

        BytesPerSector = BitConverter.ToUInt16(bootSectorBytes, 0x0B);
        SectorPerCluster = bootSectorBytes[0x0D];
        ReversedSector = BitConverter.ToUInt16(bootSectorBytes, 0x0E);
        NumberOfFAT = bootSectorBytes[0x10];
        VolumeSize = BitConverter.ToUInt32(bootSectorBytes, 0x20);
        SectorPerFAT = BitConverter.ToUInt32(bootSectorBytes, 0x24);
        StartingClusterOfRDET = BitConverter.ToUInt32(bootSectorBytes, 0x2C);
        FATType = Encoding.ASCII.GetString(bootSectorBytes, 0x52, 8);
    }

    public void ReadFAT1(FileStream fileStream)
    {
        fileStream.Seek(BytesPerSector * ReversedSector, SeekOrigin.Begin);
        int totalBytes = Convert.ToInt32(SectorPerFAT * BytesPerSector);
        byte[] FATBytes = new byte[totalBytes];
        FATTable = new UInt32[totalBytes / 4];
        fileStream.Read(FATBytes, 0, totalBytes);

        int index = 0;
        for (int i = 0; i < totalBytes; i += 4)
        {
            FATTable[index++] = BitConverter.ToUInt32(FATBytes, i);
        }
    }

    private bool IsFile(byte data)
    {
        byte mask = (byte)(1 << 5);
        // check if Archive bit(bit 5) is 1
        if ((data & mask) != 0)
        {
            return true;
        }
        return false;
    }
    private int FindLengthOfName(byte[] data, int index, int maxlength)
    {
        for (int i = 0; i < maxlength; i += 2)
        {
            UInt16 temp = BitConverter.ToUInt16(data, i + index);
            if (temp == 0x0000 || temp == 0xFFFF)
                return i;
        }
        return maxlength;
    }
    public void ReadDET(FileStream fileStream, UInt32 StartingCluster, ref List<FileManager> FileRoot)
    {
        List<UInt32> ListofCluster = FindListOfClusters(StartingCluster);
        Queue<byte[]> EntryQueue = new Queue<byte[]>();

        for (int i = 0; i < ListofCluster.Count; i++)
        {
            fileStream.Seek(OffSetWithCluster(ListofCluster[i]), SeekOrigin.Begin);
            int Count = 0;
            while (Count < SectorPerCluster * BytesPerSector)
            {
                byte[] buffer = new byte[32];
                Count += 32;
                fileStream.Read(buffer, 0, 32);
                if (buffer[0] == 0x00 || buffer[0] == 0x05 || buffer[0] == 0xE5 || buffer[0] == 0x55)
                    continue;
                EntryQueue.Enqueue(buffer);
            }
        }

        while (EntryQueue.Count > 0)
        {
            byte[] temp = EntryQueue.Dequeue();

            if (temp[0x0B] == 0x0F)
            {
                FileRoot.Add(ProcessLongName(fileStream, ref EntryQueue, temp));
            }
            else
            {
                FileRoot.Add(ProcessShortName(fileStream, temp));
            }
        }

    }
    private FileManager ProcessLongName(FileStream fileStream, ref Queue<byte[]> EntryQueue, byte[] temp)
    {
        List<string> filenamefragment = new List<string>();
        while (true)
        {
            if (temp[0x0B] != 0x0F)
                break;
            string filename = "";
            string name1 = Encoding.Unicode.GetString(temp, 0x01, FindLengthOfName(temp, 0x01, 10));
            string name2 = Encoding.Unicode.GetString(temp, 0x0E, FindLengthOfName(temp, 0x0E, 12));
            string name3 = Encoding.Unicode.GetString(temp, 0x1C, FindLengthOfName(temp, 0x1C, 4));

            filename += name1;
            filename += name2;
            filename += name3;
            filenamefragment.Add(filename);
            temp = EntryQueue.Dequeue();
        }
        FileManager tempfile = ProcessShortName(fileStream, temp);
        tempfile.MainName = "";
        for (int i = filenamefragment.Count - 1; i >= 0; i--)
        {
            tempfile.MainName += filenamefragment[i];
        }

        return tempfile;
    }
    private FileManager ProcessShortName(FileStream fileStream, byte[] temp)
    {
        if (IsFile(temp[0x0B]))
        {
            FATFile tempfile = new FATFile();
            tempfile.CloneData(temp);

            return tempfile;
        }

        FATDirectory tempdirectory = new FATDirectory();
        tempdirectory.CloneData(temp);
        if (tempdirectory.MainName != ".       " && tempdirectory.MainName != "..      ")
        {
            ReadDET(fileStream, tempdirectory.StartCluster, ref tempdirectory.Children);
        }
        return tempdirectory;

    }

    public UInt32 OffSetWithCluster(UInt32 Cluster)
    {
        return ((Cluster - 2) * SectorPerCluster + ReversedSector + NumberOfFAT * SectorPerFAT) * BytesPerSector;
    }

    private List<UInt32> FindListOfClusters(UInt32 StartCluster)
    {
        List<UInt32> result = new List<UInt32>();
        if (StartCluster == 0 || StartCluster == 1)
            return result;
        UInt32 CurrentCluster = StartCluster;
        while (true)
        {
            result.Add(CurrentCluster);

            if (FATTable[(int)CurrentCluster] == 0xFFFFFFFF || FATTable[(int)CurrentCluster] == 0x0FFFFFFF || FATTable[(int)CurrentCluster] == 0xF7FFFFFFF)
            {
                break;
            }
            CurrentCluster = FATTable[CurrentCluster];
        }
        return result;
    }

}