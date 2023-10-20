/*__ ____  _             
| __|_  / /_\  _ __ _ __ 
| _| / / / _ \| '_ \ '_ \
|___/___/_/ \_\ .__/ .__/
|  \/  |__ _| |_|__|_| _ 
| |\/| / _` | / / -_) '_|
|_|  |_\__,_|_\_\___|_|
 
(C)2022-2023 Derlidio Siqueira - Expoente Zero */

using System.Reflection;
using System.Xml.Serialization;

using EZAppMaker.Support;

namespace EZForms
{
    //  __  __          _             
    // |  \/  |__ _ _ _| |___  _ _ __ 
    // | |\/| / _` | '_| / / || | '_ \
    // |_|  |_\__,_|_| |_\_\\_,_| .__/
    //                          |_|

    public static class Bool_True
    {
        public static List<string> Values = new List<string>() { "true", "yes", "y" };
    }

    public static class Bool_False
    {
        public static List<string> Values = new List<string>() { "false", "no", "n" };
    }

    public class EZMenuItemTag
    {
        [XmlAttribute("ItemId")]
        public string ItemId { get; set; }
        [XmlAttribute("Label")]
        public string Label { get; set; }
        [XmlAttribute("Form")]
        public string Form { get; set; }
        [XmlAttribute("Icon")]
        public string Icon { get; set; }
    }

    public class EZFilterTag
    {
        [XmlAttribute("Source")]
        public string Source { get; set; }
        [XmlAttribute("Target")]
        public string Target { get; set; }
    }

    public class EZListTag
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("Source")]
        public string Source { get; set; }

        [XmlAttribute("Key")]
        public string Key { get; set; }
        [XmlAttribute("Item")]
        public string Item { get; set; }
        [XmlAttribute("Detail")]
        public string Detail { get; set; }
        [XmlAttribute("Group")]
        public string Group { get; set; }

        [XmlArrayItem("Filter")]
        public List<EZFilterTag> Filters { get; set; }
    }

    public class EZFieldTag
    {
        [XmlAttribute("Source")]
        public string Source { get; set; }
        [XmlAttribute("Input")]
        public string Input { get; set; }
        [XmlAttribute("ReadOnly")]
        public string ReadOnly { get; set; }
        [XmlAttribute("Detached")]
        public string Detached { get; set; }
        [XmlAttribute("Label")]
        public string Label { get; set; }
        [XmlAttribute("Placeholder")]
        public string Placeholder { get; set; }
        [XmlAttribute("Required")]
        public string Required { get; set; }
        [XmlAttribute("Length")]
        public int Length { get; set; }
        [XmlAttribute("Mask")]
        public string Mask { get; set; }
        [XmlAttribute("Format")]
        public string Format { get; set; }
        [XmlAttribute("Decimals")]
        public int Decimals { get; set; }
        [XmlAttribute("Keyboard")]
        public string Keyboard { get; set; }
        [XmlAttribute("List")]
        public string List { get; set; }
        [XmlAttribute("Width")]
        public double Width { get; set; } = -1;
        [XmlAttribute("Height")]
        public double Height { get; set; } = -1;
        [XmlAttribute("Searchable")]
        public string Searchable { get; set; }
        [XmlAttribute("Break")]
        public string Break { get; set; }
        [XmlAttribute("Min")]
        public double Min { get; set; }
        [XmlAttribute("Max")]
        public double Max { get; set; }

        [XmlArrayItem("Filter")]
        public List<EZFilterTag> Filters { get; set; }

        public bool IsDetached => InspectTrue(Detached);
        public bool IsReadOnly => InspectTrue(ReadOnly);
        public bool IsRequired => InspectTrue(Required);
        public bool IsSearchable => InspectTrue(Searchable);

        public bool LineBreak => !InspectFalse(Break);

        private bool InspectTrue(string property)
        {
            bool result = false;

            if (!string.IsNullOrWhiteSpace(property))
            {
                result = Bool_True.Values.Contains(property.ToLower().Trim());
            }

            return result;
        }

        private bool InspectFalse(string property)
        {
            bool result = false;

            if (!string.IsNullOrWhiteSpace(property))
            {
                result = Bool_False.Values.Contains(property.ToLower().Trim());
            }

            return result;
        }
    }

    public class EZFormTag
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }
        [XmlAttribute("Source")]
        public string Source { get; set; }
        [XmlAttribute("Label")]
        public string Label { get; set; }
        [XmlAttribute("Class")]
        public string Class { get; set; }
        [XmlAttribute("Order")]
        public string Order { get; set; }

        [XmlArrayItem("Filter")]
        public List<EZFilterTag> Filters { get; set; }

        [XmlArrayItem("Field")]
        public List<EZFieldTag> Fields { get; set; }

        [XmlArrayItem("Branch")]
        public List<EZMenuItemTag> Branches { get; set; }
    }

    public class EZMarkup
    {
        [XmlArrayItem("Item")]
        public List<EZMenuItemTag> Menu { get; set; }

        [XmlArrayItem("Form")]
        public List<EZFormTag> Forms { get; set; }

        [XmlArrayItem("List")]
        public List<EZListTag> Lists { get; set; }
    }

    //  __  __     _   _            _    
    // |  \/  |___| |_| |_  ___  __| |___
    // | |\/| / -_)  _| ' \/ _ \/ _` (_-<
    // |_|  |_\___|\__|_||_\___/\__,_/__/

    public static class EZFormsBuilder
    {
        public static EZMarkup Markup { get; set; }

        public static void Load(string file)
        {
            try
            {
                using (Stream stream = EZEmbedded.GetStream(file))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        var serializer = new XmlSerializer(typeof(EZMarkup), typeof(EZMarkup).GetNestedTypes());

                        Markup = (EZMarkup)serializer.Deserialize(reader);
                    }
                }
            }
            catch { /* Dismiss */}
        }

        public static EZFormTag GetForm(string form)
        {
            if (Markup == null) return null;

            EZFormTag frm = null;

            foreach (EZFormTag f in Markup.Forms)
            {
                if (f.Name == form)
                {
                    frm = f;
                    break;
                }
            }

            return frm;
        }

        public static EZFieldTag GetField(string form, string column)
        {
            EZFormTag frm = GetForm(form);
            EZFieldTag fld = null;

            if (frm == null) return fld;

            foreach (EZFieldTag field in frm.Fields)
            {
                if (field.Source == column)
                {
                    fld = field;
                    break;
                }
            }

            return fld;
        }

        public static string GetSource(string form)
        {
            string source = null;

            EZFormTag frm = GetForm(form);

            if (frm != null)
            {
                source = frm.Source;
            }

            return source;
        }

        public static List<EZFilterTag> GetFilters(string form)
        {
            List<EZFilterTag> filters = null;

            EZFormTag frm = GetForm(form);

            if (frm != null)
            {
                filters = frm.Filters;
            }

            return filters;
        }

        public static string GetInputType(EZFieldTag field)
        {
            string input = "none";

            if (field != null)
            {
                input = string.IsNullOrWhiteSpace(field.Input) ? "entry" : field.Input.ToLower();
            }

            return input;
        }

        public static string GetColumnLabel(string form, string column)
        {
            string label = null;

            EZFieldTag field = GetField(form, column);

            if (field != null)
            {
                label = field.Label;
            }

            return label;
        }

        public static string GetColumnMask(string form, string column)
        {
            string mask = null;

            EZFieldTag field = GetField(form, column);

            if (field != null)
            {
                mask = field.Mask;
            }

            return mask;
        }

        public static string GetPlaceholder(string form, string column)
        {
            string placeholder = null;

            EZFieldTag field = GetField(form, column);

            if (field != null)
            {
                placeholder = field.Placeholder;
            }

            return placeholder;
        }

        public static Keyboard KeyboardType(string form, string column)
        {
            Keyboard kbd = Keyboard.Default;

            EZFieldTag field = GetField(form, column);

            if (field != null)
            {
                string type = field.Keyboard?.ToLower();

                kbd = KeyboardType(type);
            }

            return kbd;
        }

        public static Keyboard KeyboardType(string type)
        {
            Keyboard kbd = Keyboard.Default;

            switch (type)
            {
                case "chat": kbd = Keyboard.Chat; break;
                case "email": kbd = Keyboard.Email; break;
                case "numeric": kbd = Keyboard.Numeric; break;
                case "plain": kbd = Keyboard.Plain; break;
                case "phone": kbd = Keyboard.Telephone; break;
                case "text": kbd = Keyboard.Text; break;
                case "url": kbd = Keyboard.Url; break;
            }

            return kbd;
        }

        public static EZListTag GetList(string form, string column)
        {
            EZListTag list = null;

            EZFieldTag fld = GetField(form, column);

            if (fld != null)
            {
                list = GetList(fld.List);
            }

            return list;
        }

        public static EZListTag GetList(string name)
        {
            EZListTag list = null;

            if (string.IsNullOrWhiteSpace(name)) return list;
            
            foreach (EZListTag lst in Markup.Lists)
            {
                if (lst.Name == name)
                {
                    list = lst;
                    break;
                }
            }

            return list;
        }

        public static bool FormExists(string form)
        {
            return GetForm(form) != null;
        }

        public static bool ListExists(string list)
        {
            return GetList(list) != null;
        }

        public static bool GetIsRequired(string form, string column)
        {
            bool required = false;

            EZFieldTag field = GetField(form, column);

            if (field != null)
            {
                required = field.IsRequired;
            }

            return required;
        }

        public static bool GetIsList(string form, string column)
        {
            bool list = false;

            EZFieldTag field = GetField(form, column);

            if (field != null)
            {
                list = !string.IsNullOrWhiteSpace(field.List);
            }

            return list;
        }
    }
}