﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TdLib
{
    class TDType
    {
        public string Name { get; set; }
        
        public string Descr { get; set; }
        
        public TDField[] Fields { get; set; }
        
        public TDType Generic { get; set; }
        
        public TDType Base { get; set; }
        
        public bool Super { get; set; }

        private const string fileTemplate = @"using System;
using Newtonsoft.Json;

namespace TdLib
{
    /// <summary>
    /// Autogenerated TDLib APIs
    /// </summary>
    public static partial class TdApi
    {
%BODY%
    }
}
";
        
        private const string classTemplate = @"
    /// <summary>
    /// %DESCR%
    /// </summary>
    public partial class %CLASS% : %BASE%
    {
%BODY%
    }
";

        private const string propTemplate = @"
        /// <summary>
        /// %DESCR%
        /// </summary>
        [JsonConverter(typeof(%CONV%))]
        [JsonProperty(""%NAME%"")]
        public %TYPE% %PROP% { get; set; }
";

        private const string metaTemplate = @"
        /// <summary>
        /// Data type for serialization
        /// </summary>
        [JsonProperty(""@type"")]
        public override string DataType { get; set; } = ""%TYPE%"";

        /// <summary>
        /// Extra data attached to the message
        /// </summary>
        [JsonProperty(""@extra"")]
        public override string Extra { get; set; }
";
        
        public string Generate()
        {
            var cls = classTemplate
                .Replace("partial class", "class")
                .Replace("%DESCR%", Descr ?? "")
                .Replace("%CLASS%", GetTypeName(Name))
                .Replace("%BODY%", GenerateMeta() + GenerateProps());

            if (Base != null && !string.Equals(Name, Base.Name, StringComparison.OrdinalIgnoreCase))
            {
                cls = cls.Replace("%BASE%", Base.Name);
                
                cls = classTemplate
                    .Replace("%DESCR%", Descr ?? "")
                    .Replace("%CLASS%", Base.Name)
                    .Replace("%BASE%", "Object")
                    .Replace("%BODY%", cls.Replace("  ", "    "));
            }
            else
            {
                cls = cls.Replace("%BASE%", "Object");
            }

            return fileTemplate.Replace("%BODY%", cls);
        }

        private string GenerateMeta()
        {
            return metaTemplate.Replace("%TYPE%", Name);
        }

        private string GenerateProps()
        {
            string props = "";
            
            foreach (var field in Fields)
            {
                var prop = GenerateProp(field);
                if (prop != null)
                {
                    props += prop;
                }
            }

            return props;
        }

        private string GenerateProp(TDField field)
        {
            return propTemplate
                .Replace("%CONV%", GenerateConv(field))
                .Replace("%TYPE%", GenerateType(field))
                .Replace("%NAME%", field.Name)
                .Replace("%DESCR%", field.Descr ?? "")
                .Replace("%PROP%", GeneratePropName(field.Name));
        }

        private string GenerateConv(TDField field)
        {
            var type = GenerateType(field);
            if (type.StartsWith("Int64"))
            {
                return "Converter.Int64";
            }
            return "Converter";
        }

        private string GeneratePropName(string str)
        {
            var arr = str.ToCharArray();
            arr[0] = arr[0].ToString().ToUpper()[0];
            str = new string(arr);

            str = str
                .Split('_')
                .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1, s.Length - 1))
                .Aggregate(string.Empty, (s1, s2) => s1 + s2);

            if (string.Equals(str, Name, StringComparison.OrdinalIgnoreCase))
            {
                str += "_";
            }

            return str;
        }

        private string GenerateType(TDField field)
        {
            string type = field.Type.Name;
            
            var n = field.Type.Name.Count(c => c == '<');
            if (n == 0)
            {
                if (field.Type.Base != null && !string.Equals(field.Type.Base.Name, type, StringComparison.OrdinalIgnoreCase))
                {
                    type = field.Type.Base.Name + "." + type;
                }

                if (builtins.Contains(type))
                {
                    return type;
                }
                
                return GetTypeName(type);
            }
            
            int begin = type.LastIndexOf('<');
            int end = type.IndexOf('>');

            type = type.Substring(begin + 1, end - begin - 1);

            // O_o
            if (field.Type.Generic?.Generic?.Generic?.Base != null) // 3rd generic level
            {
                if (!string.Equals(field.Type.Generic.Generic.Generic.Base.Name, type,
                    StringComparison.OrdinalIgnoreCase))
                {
                    type = field.Type.Generic.Generic.Generic.Base.Name + "." + type;
                }
            }
            else if (field.Type.Generic?.Generic?.Base != null) // 2nd generic level
            {
                if (!string.Equals(field.Type.Generic.Generic.Base.Name, type,
                    StringComparison.OrdinalIgnoreCase))
                {
                    type = field.Type.Generic.Generic.Base.Name + "." + type;
                }
            }
            else if (field.Type.Generic?.Base != null) // 1st generic level
            {
                if (!string.Equals(field.Type.Generic.Base.Name, type,
                    StringComparison.OrdinalIgnoreCase))
                {
                    type = field.Type.Generic.Base.Name + "." + type;
                }
            }

            bool builtin = builtins.Contains(type);
            
            for (int i = 0; i < n; i++)
            {
                type += "[]";
            }

            return builtin ? type : GetTypeName(type);
        }

        private string GetTypeName(string str)
        {
            if (str.Contains('_'))
            {
                return str
                    .Split('_')
                    .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1, s.Length - 1))
                    .Aggregate(string.Empty, (s1, s2) => s1 + s2);
            }
            else
            {
                var arr = str.ToCharArray();
                arr[0] = arr[0].ToString().ToUpper()[0];
                return new string(arr);
            }
        }
        
        private string[] builtins = new []
        {
            "bool",
            "byte",
            "int",
            "long",
            "Int64",
            "double?",
            "string"
        };
    }
}