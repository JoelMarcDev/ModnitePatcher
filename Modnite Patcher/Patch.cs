using PakLib;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Port_a_Patch
{
    public abstract class Patch
    {
        public abstract PatchType PatchType { get; }

        public string Description { get; set; }

        public string GameDirectory { get; set; }

        public ZipArchive PatchFile { get; set; }

        public abstract bool TryApplyPatch(out string failureReason);
    }

    /// <summary>
    /// Decrypts a pak.
    /// </summary>
    public class DecryptPakPatch : Patch
    {
        public override PatchType PatchType => PatchType.DecryptPak;

        public string Chunk { get; set; }

        public string Key { get; set; }

        public override bool TryApplyPatch(out string failureReason)
        {
            string path = Directory.GetFiles(GameDirectory, Chunk + ".pak", SearchOption.AllDirectories).FirstOrDefault();
            if (path == null)
            {
                failureReason = $"Can't find pak file '{Chunk}.pak'";
                return false;
            }

            try
            {
                using (var pak = Pak.OpenAsync(path).Result)
                {
                    var task = pak.DecryptAsync(Key);
                    task.Wait();
                }
            }
            catch (IncorrectEncryptionKeyException)
            {
                failureReason = "The given encryption key was not correct";
                return false;
            }

            failureReason = null;
            return true;
        }
    }

    /// <summary>
    /// Replaces a file embedded in a pak with a different file.
    /// </summary>
    public class ReplacePakFilePatch : Patch
    {
        public override PatchType PatchType => PatchType.ReplacePakFile;

        public string Chunk { get; set; }

        public string FileName { get; set; }

        public string ReplacementFileName { get; set; }

        public PaddingType Padding { get; set; }

        public override bool TryApplyPatch(out string failureReason)
        {
            string path = Directory.GetFiles(GameDirectory, Chunk + ".pak", SearchOption.AllDirectories).FirstOrDefault();
            if (path == null)
            {
                failureReason = $"Can't find pak file '{Chunk}.pak'";
                return false;
            }

            var replacementFile = PatchFile.GetEntry(ReplacementFileName);
            if (replacementFile == null)
            {
                failureReason = $"Missing file '{ReplacementFileName}' in patch";
                return false;
            }

            using (var pak = Pak.OpenAsync(path).Result)
            {
                if (pak.IsIndexEncrypted)
                {
                    failureReason = "Pak must be decrypted first";
                    return false;
                }

                foreach (var entry in pak.GetEntriesAsync().Result)
                {
                    if (entry.FileName == FileName)
                    {
                        using (var stream = replacementFile.Open())
                        using (var ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);

                            if (ms.Length > entry.Size)
                            {
                                failureReason = "Replacement file is bigger than the original file";
                                return false;
                            }
                            else if (ms.Length < entry.Size)
                            {
                                long padSize = entry.Size - ms.Length;
                                for (long i = 0; i < padSize; i++)
                                {
                                    ms.WriteByte(Padding == PaddingType.Spaces ? (byte)0x20 : (byte)0x00);
                                }
                            }

                            ms.Seek(0, SeekOrigin.Begin);
                            var task = pak.SwapEntryAsync(entry, ms);
                            task.Wait();

                            failureReason = "";
                            return true;
                        }
                    }
                }

                failureReason = $"Cannot find file '{FileName}' in pak '{Chunk}.pak'";
                return false;
            }
        }
    }

    /// <summary>
    /// Replaces bytes in an executable file in the game folder.
    /// </summary>
    public class ReplaceBytesPatch : Patch
    {
        public override PatchType PatchType => PatchType.ReplaceBytes;

        public GameFile GameFile { get; set; }

        public long Offset { get; set; }

        public SeekOrigin SeekOrigin { get; set; }

        public string OriginalBytes { get; set; }

        public string NewBytes { get; set; }

        public string Chunk { get; set; }

        public override bool TryApplyPatch(out string failureReason)
        {
            string fileName;
            if (GameFile == GameFile.PakFile)
            {
                fileName = Chunk + ".pak";
            }
            else
            {
                fileName = GameFile.GetFileName();
            }

            string path = Directory.GetFiles(GameDirectory, fileName, SearchOption.AllDirectories).FirstOrDefault();
            if (path == null)
            {
                failureReason = $"Can't find '{fileName}'";
                return false;
            }

            string originalBytesString = OriginalBytes.Replace("0x", "").Replace(" ", "").Replace("-", "");
            string newBytesString = NewBytes.Replace("0x", "").Replace(" ", "").Replace("-", "");

            if (originalBytesString.Length % 2 != 0 || newBytesString.Length % 2 != 0)
            {
                failureReason = "String of bytes does not have the correct length";
                return false;
            }

            byte[] originalBytes = originalBytesString.ConvertHexStringToBytes();
            byte[] newBytes = newBytesString.ConvertHexStringToBytes();

            using (var fs = File.Open(path, FileMode.Open))
            {
                if (fs.Length < Offset)
                {
                    failureReason = "Invalid offset";
                    return false;
                }
                else
                {
                    fs.Seek(Offset, SeekOrigin);

                    // Verify original bytes to make sure we're in the right place.
                    for (int i = 0; i < originalBytes.Length; i++)
                    {
                        if (originalBytes[i] != fs.ReadByte())
                        {
                            failureReason = $"Byte at offset {Offset} in '{fileName}' is different than expected";
                            return false;
                        }
                    }

                    fs.Seek(Offset, SeekOrigin);
                    fs.Write(newBytes);
                }
            }

            failureReason = "";
            return true;
        }
    }

    /// <summary>
    /// Deletes an executable file in the game folder.
    /// </summary>
    public class DeleteFilePatch : Patch
    {
        public override PatchType PatchType => PatchType.DeleteFile;

        public GameFile GameFile { get; set; }

        public override bool TryApplyPatch(out string failureReason)
        {
            string fileName = GameFile.GetFileName();
            string path = Directory.GetFiles(GameDirectory, fileName, SearchOption.AllDirectories).FirstOrDefault();
            if (path == null)
            {
                failureReason = $"Can't find '{fileName}'";
                return false;
            }

            File.Delete(path);

            failureReason = "";
            return true;
        }
    }
}