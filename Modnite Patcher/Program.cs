using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Port_a_Patch
{
    class Program
    {
        const int CurrentVersion = 2;

        static void Main(string[] args)
        {
            Console.Title = "Modnite Patcher";

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Modnite Patcher");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("https://modnite.net");
            Console.WriteLine();
            Console.ResetColor();

            string patchFile;
            if (args.Length > 0)
            {
                patchFile = args[0];
            }
            else
            {
                Console.Write("Path to patch file: ");
                Console.ForegroundColor = ConsoleColor.White;
                patchFile = Console.ReadLine().Replace("\"", "");
                Console.ResetColor();
            }

            string gameFolder;
            Console.Write("Path to game folder: ");
            Console.ForegroundColor = ConsoleColor.White;
            gameFolder = Console.ReadLine().Replace("\"", "");
            if (!Directory.Exists(gameFolder))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Game directory doesn't exist");
                Console.ReadLine();
                return;
            }
            Console.WriteLine();
            Console.ResetColor();

            dynamic manifest;
            using (var zip = ZipFile.Open(patchFile, ZipArchiveMode.Read))
            {
                // Extract manifest.
                var manifestFile = zip.GetEntry("manifest.json");
                using (var stream = manifestFile.Open())
                {
                    // For some reason, we can't access stream.Length?
                    byte[] buffer = new byte[32768];
                    using (var ms = new MemoryStream())
                    {
                        while (true)
                        {
                            int read = stream.Read(buffer, 0, buffer.Length);
                            if (read <= 0) break;
                            ms.Write(buffer, 0, read);
                        }

                        manifest = JObject.Parse(Encoding.UTF8.GetString(ms.ToArray()));
                    }
                }

                // Read manifest.
                var patches = new List<Patch>();
                try
                {
                    int version = Convert.ToInt32(manifest.Version);
                    if (version > CurrentVersion)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("This manifest is for a newer version of Modnite Patcher. Please update to the latest version.");
                        Console.ReadLine();
                        return;
                    }

                    Console.WriteLine($"Patch '{manifest.Name}' will make the following changes: ");

                    foreach (var patchInfo in manifest.Patches)
                    {
                        Type patchType = null;
                        switch ((PatchType)Enum.Parse(typeof(PatchType), patchInfo.PatchType.ToString()))
                        {
                            case PatchType.DecryptPak:
                                patchType = typeof(DecryptPakPatch);
                                break;

                            case PatchType.ReplaceBytes:
                                patchType = typeof(ReplaceBytesPatch);
                                break;

                            case PatchType.ReplacePakFile:
                                patchType = typeof(ReplacePakFilePatch);
                                break;

                            case PatchType.DeleteFile:
                                patchType = typeof(DeleteFilePatch);
                                break;
                        }

                        if (patchType != null)
                        {
                            Patch patch = JsonConvert.DeserializeObject(patchInfo.ToString(), patchType);
                            patch.GameDirectory = gameFolder;
                            patch.PatchFile = zip;
                            patches.Add(patch);

                            string parenthesize(string value)
                            {
                                if (!string.IsNullOrWhiteSpace(value))
                                    return '(' + value + ')';
                                else
                                    return "";
                            }

                            switch (patch)
                            {
                                case DecryptPakPatch p:
                                    Console.WriteLine($" - Decrypt '{p.Chunk}.pak' {parenthesize(p.Description)}");
                                    break;

                                case ReplaceBytesPatch p:
                                    if (p.GameFile == GameFile.PakFile)
                                    {
                                        Console.WriteLine($" - Edit '{p.Chunk}.pak' {parenthesize(p.Description)}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($" - Edit '{p.GameFile.GetFileName()}' {parenthesize(p.Description)}");
                                    }
                                    break;

                                case ReplacePakFilePatch p:
                                    Console.WriteLine($" - Edit '{p.FileName}' in '{p.Chunk}.pak' {parenthesize(p.Description)}");
                                    break;

                                case DeleteFilePatch p:
                                    Console.WriteLine($" - Delete '{p.GameFile.GetFileName()}' {parenthesize(p.Description)}");
                                    break;
                            }
                        }
                    }
                    Console.WriteLine();
                }
                catch (Exception)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR: Invalid patch manifest!");

#if DEBUG
                    throw;
#else
                    return;
#endif
                }

                // Confirmation.
                Console.Write("Apply patch? (y/n): ");
                Console.ForegroundColor = ConsoleColor.White;
                string choice = Console.ReadLine().ToLowerInvariant();
                Console.ResetColor();
                if (choice != "y")
                {
                    Console.WriteLine("Patch cancelled");
                    Console.ReadLine();
                    return;
                }
                Console.WriteLine();

                // Apply patches.
                foreach (Patch patch in patches)
                {
                    if (patch.TryApplyPatch(out string reason))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"Applied {patch.PatchType}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"Skipped {patch.PatchType} because: {reason}");
                    }
                }
                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Done!");
            Console.ReadLine();
        }
    }
}
