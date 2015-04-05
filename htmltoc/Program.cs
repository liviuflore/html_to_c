using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace htmltoc
{
    class Program
    {
        static void Usage()
        {
            Console.WriteLine("USAGE:");
            Console.WriteLine("\t" + System.AppDomain.CurrentDomain.FriendlyName + " source_dir [destination_dir]");
        }

        static void Main(string[] args)
        {
            if (args.Count() < 1 || args.Count() > 2)
            {
                Usage();
                return;
            }

            string source = args[0];
            string destination = args[0];
            if (args.Count() == 2)
                destination = args[1];

            string[] files = GetFiles(source, "*.html|*.css|*.png", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                ConvertFile(file, destination);
            }

            WriteWebpages(files, destination);
        }

        private static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            string[] searchPatterns = searchPattern.Split('|');
            List<string> files = new List<string>();
            foreach (string sp in searchPatterns)
                files.AddRange(System.IO.Directory.GetFiles(path, sp, searchOption));
            files.Sort();
            return files.ToArray();
        }

        private static void WriteWebpages(string[] files, string destination)
        {
            // webpages.h
            using (TextWriter tw = new StreamWriter(destination + "\\webpages.h"))
            {
                tw.WriteLine("#ifndef __WEBPAGES_H__");
                tw.WriteLine("#define __WEBPAGES_H__");
                tw.WriteLine();
                tw.WriteLine("struct www_webpage {");
                tw.WriteLine("\tchar* name;");
                tw.WriteLine("\tchar* page;");
                tw.WriteLine("\tint size;");
                tw.WriteLine("\tint (*action)(char* in, int size, char* out);");
                tw.WriteLine("};");
                tw.WriteLine();
                foreach (string file in files)
                {
                    tw.WriteLine("extern const char www_" + Path.GetFileName(file).Replace('.', '_') + "_array[];");
                    tw.WriteLine("extern const int www_" + Path.GetFileName(file).Replace('.', '_') + "_length;");
                }
                tw.WriteLine();
                tw.WriteLine("struct www_webpage* www_webpages_get(char* name);");
                tw.WriteLine("void www_webpages_register_action(char* page, int(*action)(char*, int, char*));");
                tw.WriteLine("void www_webpages_init(void);");
                tw.WriteLine();
                tw.WriteLine("#endif /* __WEBPAGES_H__ */");
            }
            // webpages.c
            using (TextWriter tw = new StreamWriter(destination + "\\webpages.c"))
            {
                tw.WriteLine("#ifdef WIN32");
                tw.WriteLine("#include <stdio.h>");
                tw.WriteLine("#include <stdlib.h>");
                tw.WriteLine("#include <string.h>");
                tw.WriteLine("#else");
                tw.WriteLine("#include \"esp_common.h\"");
                tw.WriteLine("#endif");
                tw.WriteLine("#include \"webpages.h\"");
                tw.WriteLine();
                tw.WriteLine();
                tw.WriteLine("#define WWW_MAX_PAGES 16");
                tw.WriteLine();
                tw.WriteLine("int www_webpages_count = 0;");
                tw.WriteLine("struct www_webpage www_webpages[WWW_MAX_PAGES] = { 0 };");
                tw.WriteLine();
                tw.WriteLine("int www_webpages_add(char* name, const char* page, int size)");
                tw.WriteLine("{");
                tw.WriteLine("\tif (www_webpages_count >= WWW_MAX_PAGES) {");
                tw.WriteLine("\t\tprintf(\"Could not add more pages. Limit reached %d\\n\", WWW_MAX_PAGES);");
                tw.WriteLine("\t\treturn -1;");
                tw.WriteLine("\t}");
                tw.WriteLine("\twww_webpages[www_webpages_count].name = name;");
                tw.WriteLine("\twww_webpages[www_webpages_count].page = (char*)page;");
                tw.WriteLine("\twww_webpages[www_webpages_count].size = size;");
                tw.WriteLine("\twww_webpages[www_webpages_count].action = NULL;");
                tw.WriteLine("\twww_webpages_count++;");
                tw.WriteLine("\treturn 0;");
                tw.WriteLine("}");
                tw.WriteLine();
                tw.WriteLine("struct www_webpage* www_webpages_get(char* name)");
                tw.WriteLine("{");
                tw.WriteLine("\tint i;");
                tw.WriteLine("\tfor (i = 0; i < www_webpages_count; i ++) {");
                tw.WriteLine("\t\tif (strcmp(name, www_webpages[i].name) == 0) {");
                tw.WriteLine("\t\t\treturn &(www_webpages[i]);");
                tw.WriteLine("\t\t}");
                tw.WriteLine("\t}");
                tw.WriteLine("\treturn NULL;");
                tw.WriteLine("}");
                tw.WriteLine();
                tw.WriteLine("void www_webpages_register_action(char* page, int(*action)(char*, int, char*))");
                tw.WriteLine("{");
                tw.WriteLine("\tstruct www_webpage *wp = www_webpages_get(page);");
                tw.WriteLine("\tif (wp != NULL && action != NULL) {");
                tw.WriteLine("\t\twp->action = action;");
                tw.WriteLine("\t}");
                tw.WriteLine("}");
                tw.WriteLine();
                tw.WriteLine("void www_webpages_init(void)");
                tw.WriteLine("{");
                foreach (string file in files)
                {
                    tw.Write("\twww_webpages_add(\"/" + Path.GetFileName(file) + "\", ");
                    tw.Write("www_" + Path.GetFileName(file).Replace('.', '_') + "_array, ");
                    tw.Write("www_" + Path.GetFileName(file).Replace('.', '_') + "_length);");
                    tw.WriteLine();
                }
                tw.WriteLine("}");
                tw.WriteLine();
            }
        }

        private static string GetFileHeader(string file)
        {
            if (Path.GetExtension(file) == ".html")
                return "\"HTTP/1.1 200 OK\\r\\nContent-type: text/html\\r\\n\\r\\n\"";
            else if (Path.GetExtension(file) == ".css")
                return "\"HTTP/1.1 200 OK\\r\\nContent-type: text/css\\r\\n\\r\\n\"";
            else if (Path.GetExtension(file) == ".png")
                return "HTTP/1.1 200 OK\r\nContent-type: image/png\r\n\r\n";
            else
                return "";
        }

        private static int GetFile(string file, List<string> output)
        {
            if (Path.GetExtension(file) == ".html")
                return GetTxtFile(file, output);
            else if (Path.GetExtension(file) == ".css")
                return GetTxtFile(file, output);
            else if (Path.GetExtension(file) == ".png")
                return GetBinFile(file, output);
            else
                return 0;
        }

        private static void ConvertFile(string file, string destination)
        {
            try
            {
                string destination_file = destination + "\\" + Path.GetFileName(file).Replace('.', '_') + ".c";
                Directory.CreateDirectory(destination);
                TextWriter tw = new StreamWriter(destination_file);
                List<string> file_content = new List<string>();
                int size = 0;

                tw.Write("const char www_" + Path.GetFileName(file).Replace('.', '_') + "_array[] = ");
                size = GetFile(file, file_content);
                foreach (string line in file_content)
                {
                    tw.WriteLine(line);
                }
                tw.WriteLine(";");
                tw.WriteLine("const int www_" + Path.GetFileName(file).Replace('.', '_') + "_length = " + size + ";");
                tw.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception while converting file: " + file);
                Console.WriteLine("\t" + ex.Message);
            }
        }


        private static int GetTxtFile(string file, List<string> output)
        {
            int total_size = 0;
            string header = GetFileHeader(file);
            output.Add(header);
            total_size += header.Length - 7;

            TextReader tr = new StreamReader(file);
            string line;
            while ((line = tr.ReadLine()) != null)
            {
                byte[] bytes = Encoding.Default.GetBytes(line);
                string str = Encoding.ASCII.GetString(bytes);
                str = str.Replace("\"", "\\\"");
                total_size += str.Length - str.Count(x => x == '\"') + 1;
                output.Add("\t\"" + str + "\\n\"");
            }
            return total_size;
        }

        private static int GetBinFile(string file, List<string> output)
        {
            BinaryReader br = new BinaryReader(File.Open(file, FileMode.Open));
            string header = GetFileHeader(file);
            int i;
            byte b;
            int total_size = 0;
            int line_size = 0;
            string str = "";
            output.Add("{");
            for (i = 0; i < header.Length; i++)
            {
                b = (byte)header[i];
                str += "0x" + String.Format("{0,0:x2}", b) + ", ";
                total_size++;
            }
            output.Add("\t" + str);
            str = "";
            try
            {
                while (true)
                {
                    b = br.ReadByte();
                    if (line_size == 32)
                    {
                        line_size = 0;
                        output.Add("\t" + str);
                        str = "";
                    }
                    str += "0x" + String.Format("{0,0:x2}", b) + ", ";
                    total_size++;
                    line_size++;
                }
            }
            catch (EndOfStreamException) { };
            output.Add("}");
            return total_size;
        }

        //private static void ConvertTextFile(string file, string destination)
        //{
        //    try
        //    {
        //        string destination_file = destination + "\\" + Path.GetFileName(file).Replace('.','_') + ".c";
        //        Directory.CreateDirectory(destination);
        //        TextReader tr = new StreamReader(file);
        //        TextWriter tw = new StreamWriter(destination_file);

        //        tw.Write("const char www_" + Path.GetFileName(file).Replace('.', '_') + "_array[] = ");
        //        string line;
        //        int total_size = 0;
        //        while((line = tr.ReadLine()) != null)
        //        {
        //            byte[] bytes = Encoding.Default.GetBytes(line);
        //            string str = Encoding.ASCII.GetString(bytes);
        //            str = str.Replace("\"", "\\\"");
        //            //str = str.Replace("\\", "\\\\");
        //            total_size += str.Length;
        //            tw.WriteLine();
        //            tw.Write("\t\"" + str + "\\n\"");
        //        }
        //        tw.WriteLine(";");
        //        tw.WriteLine("const int www_" + Path.GetFileName(file).Replace('.', '_') + "_length = " + total_size + ";");
        //        tw.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("exception while converting file: " + file);
        //        Console.WriteLine("\t" + ex.Message);
        //    }
        //}

        //private static void ConvertBinFile(string file, string destination)
        //{
        //    try
        //    {
        //        string destination_file = destination + "\\" + Path.GetFileName(file).Replace('.', '_') + ".c";
        //        Directory.CreateDirectory(destination);
        //        BinaryReader br = new BinaryReader(File.Open(file, FileMode.Open));
        //        TextWriter tw = new StreamWriter(destination_file);

        //        tw.WriteLine("const char www_" + Path.GetFileName(file).Replace('.', '_') + "_array[] = {");
        //        byte b;
        //        int total_size = 0;
        //        int line_size = 0;
        //        tw.Write("\t");
        //        try
        //        {
        //            while (true)
        //            {
        //                b = br.ReadByte();
        //                if (line_size == 32)
        //                {
        //                    tw.WriteLine();
        //                    tw.Write("\t");
        //                    line_size = 0;
        //                }
        //                string str = "0x" + String.Format("{0,0:x2}", b) + ", ";
        //                total_size++;
        //                line_size++;

        //                tw.Write(str);
        //            }
        //        }
        //        catch (EndOfStreamException) { };
        //        tw.WriteLine("};");
        //        tw.WriteLine("const int www_" + Path.GetFileName(file).Replace('.', '_') + "_length = " + total_size + ";");
        //        tw.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine("exception while converting file: " + file);
        //        Console.WriteLine("\t" + ex.Message);
        //    }
        //}
    }
}
