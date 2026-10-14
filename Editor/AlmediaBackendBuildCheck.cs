using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AlmediaLink.Editor
{
    /// <summary>
    /// Fails a non-development player build whose compiled scripts contain a URL of the
    /// testing backend. Runs after player script compilation, before stripping and IL2CPP.
    /// </summary>
    internal sealed class AlmediaBackendBuildCheck : IPostBuildPlayerScriptDLLs
    {
        internal const string Marker = "gazebo";

        private static readonly string[] SkippedAssemblyPrefixes =
        {
            "UnityEngine", "UnityEditor", "Unity.", "System", "mscorlib", "netstandard", "Mono.", "Microsoft."
        };

        public int callbackOrder => 0;

        public void OnPostBuildPlayerScriptDLLs(BuildReport report)
        {
            if (report.summary.options.HasFlag(BuildOptions.Development)) return;

            var dlls = PlayerAssemblies();
            if (dlls.Count == 0)
            {
                Debug.LogWarning("[Almedia] Player assemblies not found. The testing-backend check did not run.");
                return;
            }

            var hit = FindTestingBackendUrl(dlls);
            if (hit == null) return;

            throw new BuildFailedException(
                $"[Almedia] {hit.Value.assembly} contains the testing backend URL \"{hit.Value.literal}\". " +
                "A release build cannot talk to the testing backend. Set BackendUrl under #if DEVELOPMENT_BUILD only.");
        }

        internal static bool IsTestingBackendUrl(string literal)
        {
            var s = literal.Trim();
            return (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                   && s.IndexOf(Marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>The first testing backend URL literal in the given assemblies, or null.</summary>
        internal static (string assembly, string literal)? FindTestingBackendUrl(IEnumerable<string> dllPaths)
        {
            foreach (var path in dllPaths)
            {
                var name = Path.GetFileName(path);
                if (IsSkipped(name)) continue;

                List<string> literals;
                try
                {
                    literals = ManagedStringLiterals.Read(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Almedia] {name} could not be read for the testing-backend check: {e.Message}");
                    continue;
                }

                foreach (var literal in literals)
                    if (IsTestingBackendUrl(literal))
                        return (name, literal);
            }

            return null;
        }

        private static bool IsSkipped(string fileName)
        {
            foreach (var prefix in SkippedAssemblyPrefixes)
                if (fileName.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// The managed assemblies of this build. The staging area is rebuilt on every build and
        /// holds exactly what ships.
        /// </summary>
        private static List<string> PlayerAssemblies()
        {
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            var staging = Path.Combine(project, "Temp/StagingArea/Data/Managed");
            if (Directory.Exists(staging))
            {
                var shipped = new List<string>(Directory.GetFiles(staging, "*.dll"));
                if (shipped.Count > 0) return shipped;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dlls = new List<string>();
            foreach (var relative in new[] { "Library/Bee/PlayerScriptAssemblies", "Library/PlayerScriptAssemblies" })
            {
                var folder = Path.Combine(project, relative);
                if (!Directory.Exists(folder)) continue;

                foreach (var dll in Directory.GetFiles(folder, "*.dll"))
                    if (seen.Add(Path.GetFileName(dll)))
                        dlls.Add(dll);
            }

            return dlls;
        }
    }

    /// <summary>Reads the string literals of a managed assembly from its #US metadata heap.</summary>
    internal static class ManagedStringLiterals
    {
        private const uint PeSignature = 0x00004550;
        private const uint MetadataSignature = 0x424A5342;
        private const ushort Pe32Plus = 0x20B;
        private const int CliHeaderDirectory = 14;

        /// <summary>Every literal in the file. Empty when the file is not a managed PE image.</summary>
        public static List<string> Read(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var result = new List<string>();
            if (bytes.Length < 0x40) return result;

            var pe = BitConverter.ToInt32(bytes, 0x3C);
            if (pe < 0 || pe + 24 > bytes.Length || BitConverter.ToUInt32(bytes, pe) != PeSignature) return result;

            int sections = BitConverter.ToUInt16(bytes, pe + 6);
            int optionalHeaderSize = BitConverter.ToUInt16(bytes, pe + 20);
            var optionalHeader = pe + 24;
            var directories = optionalHeader + (BitConverter.ToUInt16(bytes, optionalHeader) == Pe32Plus ? 112 : 96);
            var cliDirectory = directories + CliHeaderDirectory * 8;
            if (cliDirectory + 8 > bytes.Length) return result;

            var cliRva = BitConverter.ToUInt32(bytes, cliDirectory);
            if (cliRva == 0) return result;

            var sectionTable = optionalHeader + optionalHeaderSize;
            var cli = RvaToOffset(bytes, sectionTable, sections, cliRva);
            if (cli < 0 || cli + 16 > bytes.Length) return result;

            var metadata = RvaToOffset(bytes, sectionTable, sections, BitConverter.ToUInt32(bytes, cli + 8));
            if (metadata < 0 || metadata + 20 > bytes.Length
                || BitConverter.ToUInt32(bytes, metadata) != MetadataSignature) return result;

            var versionLength = BitConverter.ToInt32(bytes, metadata + 12);
            var streamCountOffset = metadata + 16 + versionLength + 2;
            if (streamCountOffset + 2 > bytes.Length) return result;
            int streams = BitConverter.ToUInt16(bytes, streamCountOffset);

            var header = streamCountOffset + 2;
            for (var i = 0; i < streams && header + 8 < bytes.Length; i++)
            {
                var offset = BitConverter.ToUInt32(bytes, header);
                var size = BitConverter.ToUInt32(bytes, header + 4);
                var nameStart = header + 8;
                var nameEnd = nameStart;
                while (nameEnd < bytes.Length && bytes[nameEnd] != 0) nameEnd++;
                var name = Encoding.ASCII.GetString(bytes, nameStart, nameEnd - nameStart);
                header = nameStart + ((nameEnd - nameStart + 1 + 3) & ~3);

                if (name != "#US") continue;

                ReadUserStrings(bytes, metadata + (int)offset, (int)size, result);
                break;
            }

            return result;
        }

        private static int RvaToOffset(byte[] bytes, int sectionTable, int sections, uint rva)
        {
            for (var i = 0; i < sections; i++)
            {
                var s = sectionTable + i * 40;
                if (s + 40 > bytes.Length) return -1;

                var virtualSize = BitConverter.ToUInt32(bytes, s + 8);
                var virtualAddress = BitConverter.ToUInt32(bytes, s + 12);
                var rawSize = BitConverter.ToUInt32(bytes, s + 16);
                var rawPointer = BitConverter.ToUInt32(bytes, s + 20);
                if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, rawSize))
                    return (int)(rva - virtualAddress + rawPointer);
            }

            return -1;
        }

        // Entries are a compressed length, (length - 1) / 2 UTF-16 code units and one trailing byte.
        private static void ReadUserStrings(byte[] bytes, int start, int size, List<string> into)
        {
            var end = Math.Min((long)start + size, bytes.Length);
            var p = start + 1;
            while (p < end)
            {
                var length = ReadCompressedUInt(bytes, ref p);
                if (length < 0 || p + length > end) break;
                if (length > 1) into.Add(Encoding.Unicode.GetString(bytes, p, (length - 1) & ~1));
                p += length;
            }
        }

        private static int ReadCompressedUInt(byte[] bytes, ref int p)
        {
            var b = bytes[p];
            if ((b & 0x80) == 0)
            {
                p += 1;
                return b;
            }

            if ((b & 0xC0) == 0x80)
            {
                if (p + 2 > bytes.Length) return -1;
                var value = ((b & 0x3F) << 8) | bytes[p + 1];
                p += 2;
                return value;
            }

            if ((b & 0xE0) == 0xC0)
            {
                if (p + 4 > bytes.Length) return -1;
                var value = ((b & 0x1F) << 24) | (bytes[p + 1] << 16) | (bytes[p + 2] << 8) | bytes[p + 3];
                p += 4;
                return value;
            }

            return -1;
        }
    }
}
