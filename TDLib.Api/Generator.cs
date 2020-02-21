using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TdLib
{
    class Generator
    {
        static List<TDType> _types = new List<TDType>();
        static List<TDFunc> _funcs = new List<TDFunc>();
        
        static void Main(string[] args)
        {
            GenerateTypes();
            GenerateFuncs();
        }
        
        private static string GetFileName(string str)
        {
            var arr = str.ToCharArray();
            arr[0] = arr[0].ToString().ToUpper()[0];
            return new string(arr);
        }

        private static void GenerateTypes()
        {
            var lines = System.IO.File.ReadAllLines("./types.tl")
                .Select(l => l.Replace(":bytes ", ":vector<byte> "))
                .ToArray();

            var chunks = new List<string[]>();
            var currentChunk = new List<string>();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    chunks.Add(currentChunk.ToArray());
                    currentChunk.Clear();
                }
                else
                {
                    currentChunk.Add(line);
                }
            }
            
            foreach (var chunk in chunks)
            {
                ParseType(chunk);
            }

            var builtins = new []
            {
                "bool",
                "byte",
                "int",
                "long",
                "Int64",
                "double?",
                "string"
            };
            
            foreach (var type in _types)
            {
                if (builtins.Contains(type.Name))
                {
                    continue;
                }
                
                if (!type.Name.Contains("<") && !type.Super)
                {
                    var str = type.Generate();
                    System.IO.File.WriteAllText("./Objects/"+GetFileName(type.Name)+".cs", str);
                }
            }
        }

        private static void GenerateFuncs()
        {
            var lines = System.IO.File.ReadAllLines("./methods.tl")
                .Select(l => l.Replace(":bytes ", ":vector<byte> "))
                .ToArray();
            
            var chunks = new List<string[]>();
            var currentChunk = new List<string>();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    chunks.Add(currentChunk.ToArray());
                    currentChunk.Clear();
                }
                else
                {
                    currentChunk.Add(line);
                }
            }
            
            foreach (var chunk in chunks)
            {
                ParseFunc(chunk);
            }

            foreach (var func in _funcs)
            {
                var str = func.Generate();
                System.IO.File.WriteAllText("./Functions/"+GetFileName(func.Name)+".cs", str);
            }
        }

        private static TDType ParseType(string[] chunks)
        {
            var line = chunks.FirstOrDefault(l => !l.StartsWith("//"));
            
            if (line != null)
            {
                var descrs = GetDescrs(chunks);
                var parts = line.Split(new[] {" = ", " "}, StringSplitOptions.RemoveEmptyEntries);

                var type = GetType(GetName(parts), descrs, true);
                type.Fields = GetFields(parts, descrs);
                type.Base = GetType(GetUnion(parts), descrs);
                type.Base.Super = true;
            
                return type;
            }

            return null;
        }

        private static TDFunc ParseFunc(string[] chunks)
        {
            var line = chunks.FirstOrDefault(l => !l.StartsWith("//"));

            if (line != null)
            {
                var descrs = GetDescrs(chunks);
                var parts = line.Split(new[] {" = ", " "}, StringSplitOptions.RemoveEmptyEntries);

                var func = GetFunc(GetName(parts), descrs);
                func.Args = GetFields(parts, descrs);
                func.Result = GetType(GetUnion(parts), descrs);
                func.Result.Super = true;

                return func;
            }

            return null;
        }

        private static string GetName(string[] parts)
        {
            return GetName(parts[0]);
        }

        private static string GetName(string part)
        {
            switch (part)
            {
                case "Bool":
                    return "bool";
                case "byte":
                    return "byte";
                case "int32":
                    return "int";
                case "int53":
                    return "long";
                case "int64":
                    return "Int64";
                case "double":
                    return "double?";
            }
            
            return part
                .Replace("<Bool>", "<bool>")
                .Replace("<byte>", "<byte>")
                .Replace("<int32>", "<int>")
                .Replace("<int53>", "<long>")
                .Replace("<int64>", "<Int64>")
                .Replace("<double>", "<double>");
        }

        private static string GetUnion(string[] parts)
        {
            return parts[parts.Length - 1].Replace(";", "");
        }

        private static TDField[] GetFields(string[] parts, Dictionary<string, string> descrs)
        {
            var list = new List<TDField>();
            
            for (int i = 1; i < parts.Length - 1; i++)
            {
                list.Add(GetField(parts[i], descrs));
            }

            return list.ToArray();
        }

        private static TDField GetField(string str, Dictionary<string, string> descrs)
        {
            var parts = str.Split(':');

            descrs.TryGetValue(parts[0].ToLowerInvariant(), out var descr);
            
            return new TDField
            {
                Name = parts[0],
                Descr = descr,
                Type = GetType(parts[1], descrs)
            };
        }

        private static TDType GetType(string str, Dictionary<string, string> descrs, bool primary = false)
        {
            TDType type;
            int begin = str.IndexOf('<');
            int end = str.LastIndexOf('>');
            
            if (begin >= 0)
            {
                var s = str.Substring(begin + 1, end - begin - 1);

                type = _types.ToList().FirstOrDefault(t => t.Name == str && t.Generic == GetType(s, descrs));
                if (type == null)
                {
                    type = new TDType
                    {
                        Name = GetName(str),
                        Fields = new TDField[0],
                        Generic = GetType(s, descrs)
                    };
                    _types.Add(type);
                }
            }
            else
            {
                type = _types.ToList().FirstOrDefault(t => t.Name == str);
                if (type == null)
                {
                    type = new TDType
                    {
                        Name = GetName(str),
                        Fields = new TDField[0]
                    };
                    _types.Add(type);
                }
            }

            if (primary)
            {
                descrs.TryGetValue("description", out var descr);
                type.Descr = descr;
            }

            return type;
        }

        private static TDFunc GetFunc(string str, Dictionary<string, string> descrs)
        {
            descrs.TryGetValue("description", out var descr);
            
            var func = new TDFunc
            {
                Name = str,
                Descr = descr
            };
            _funcs.Add(func);

            return func;
        }

        private static Dictionary<string, string> GetDescrs(string[] chunks)
        {
            var descrs = new Dictionary<string, string>();
            var re = new Regex("(@[a-zA-Z_]+)[ ]+([^@]+)");
            
            foreach (var chunk in chunks)
            {
                var matches = re.Matches(chunk);
                foreach (Match match in matches)
                {
                    var key = match.Groups[1].Value.ToLowerInvariant().Substring(1);
                    var val = match.Groups[2].Value;
                    descrs[key] = val;
                }
            }

            return descrs;
        }
    }
}