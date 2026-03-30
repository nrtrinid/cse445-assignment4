using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using Newtonsoft.Json;

/**
 * This template file is created for ASU CSE445 Distributed SW Dev Assignment 4.
 * Please do not modify or delete any existing class/variable/method names. However, you can add more variables and functions.
 * Uploading this file directly will not pass the autograder's compilation check, resulting in a grade of 0.
 * **/


namespace ConsoleApp1
{


    public class Submission
    {
        public static string xmlURL = "https://nrtrinid.github.io/cse445-assignment4/NationalParks.xml";
        public static string xmlErrorURL = "https://nrtrinid.github.io/cse445-assignment4/NationalParksErrors.xml";
        public static string xsdURL = "https://nrtrinid.github.io/cse445-assignment4/NationalParks.xsd";

        public static void Main(string[] args)
        {
            string result = Verification(xmlURL, xsdURL);
            Console.WriteLine(result);


            result = Verification(xmlErrorURL, xsdURL);
            Console.WriteLine(result);


            result = Xml2Json(xmlURL);
            Console.WriteLine(result);
        }

        // Q2.1
        public static string Verification(string xmlUrl, string xsdUrl)
        {
            List<string> errors = new List<string>();
            string xmlText = string.Empty;
            string resolvedXmlUrl = ResolvePathOrUrl(xmlUrl);
            string resolvedXsdUrl = ResolvePathOrUrl(xsdUrl);

            try
            {
                xmlText = DownloadContent(resolvedXmlUrl);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            try
            {
                XmlSchemaSet schemas = new XmlSchemaSet();
                schemas.Add(null, resolvedXsdUrl);

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.ValidationType = ValidationType.Schema;
                settings.Schemas = schemas;
                settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
                settings.ValidationEventHandler += delegate (object sender, ValidationEventArgs e)
                {
                    AddError(errors, FormatValidationMessage(e));
                };

                using (StringReader stringReader = new StringReader(xmlText))
                using (XmlReader reader = XmlReader.Create(stringReader, settings, resolvedXmlUrl))
                {
                    while (reader.Read())
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                AddError(errors, ex.Message);
            }

            AddNationalParkChecks(xmlText, errors);

            if (errors.Count == 0)
            {
                return "No errors are found";
            }

            return string.Join(Environment.NewLine, errors);
        }

        public static string Xml2Json(string xmlUrl)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(DownloadContent(xmlUrl));
            string jsonText = JsonConvert.SerializeXmlNode(doc.DocumentElement, Newtonsoft.Json.Formatting.Indented, false);
            return jsonText;
        }

        // Helper method to download content from URL
        private static string DownloadContent(string url)
        {
            using (System.Net.WebClient client = new System.Net.WebClient())
            {
                return client.DownloadString(ResolvePathOrUrl(url));
            }
        }

        private static string ResolvePathOrUrl(string pathOrUrl)
        {
            if (Uri.IsWellFormedUriString(pathOrUrl, UriKind.Absolute))
            {
                return pathOrUrl;
            }

            string fullPath = Path.GetFullPath(pathOrUrl);
            if (!File.Exists(fullPath))
            {
                fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, pathOrUrl);
            }

            return new Uri(fullPath).AbsoluteUri;
        }

        private static string FormatValidationMessage(ValidationEventArgs e)
        {
            if (e.Exception != null && e.Exception.LineNumber > 0)
            {
                return "Line " + e.Exception.LineNumber + ", Position " + e.Exception.LinePosition + ": " + e.Message;
            }

            return e.Message;
        }

        private static void AddError(List<string> errors, string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && !errors.Contains(message))
            {
                errors.Add(message);
            }
        }

        private static void AddNationalParkChecks(string xmlText, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(xmlText) || !xmlText.Contains("NationalPark"))
            {
                return;
            }

            Match rootMatch = Regex.Match(xmlText, @"^\s*<\?xml.*?\?>\s*<\s*(?<name>[A-Za-z_][\w\-\.]*)", RegexOptions.Singleline);
            if (!rootMatch.Success)
            {
                rootMatch = Regex.Match(xmlText, @"<\s*(?<name>[A-Za-z_][\w\-\.]*)");
            }

            string rootName = rootMatch.Success ? rootMatch.Groups["name"].Value : string.Empty;
            if (rootName == "NationalPark")
            {
                AddError(errors, "Line " + GetLineNumber(xmlText, rootMatch.Index) + ": The root element name should be NationalParks.");
            }

            MatchCollection parkMatches = Regex.Matches(xmlText, @"<NationalPark\b[^>]*>", RegexOptions.IgnoreCase);
            int startIndex = rootName == "NationalPark" ? 1 : 0;

            for (int i = startIndex; i < parkMatches.Count; i++)
            {
                Match parkMatch = parkMatches[i];
                int endIndex = xmlText.IndexOf("</NationalPark>", parkMatch.Index, StringComparison.OrdinalIgnoreCase);
                if (endIndex < 0)
                {
                    endIndex = xmlText.Length;
                }
                else
                {
                    endIndex += "</NationalPark>".Length;
                }

                string parkText = xmlText.Substring(parkMatch.Index, endIndex - parkMatch.Index);

                if (Regex.Matches(parkText, @"<Name\b", RegexOptions.IgnoreCase).Count > 1)
                {
                    AddError(errors, "Line " + GetLineNumber(xmlText, parkMatch.Index) + ": One NationalPark element has more than one Name element.");
                }

                if (Regex.Matches(parkText, @"<Phone\b", RegexOptions.IgnoreCase).Count == 0)
                {
                    AddError(errors, "Line " + GetLineNumber(xmlText, parkMatch.Index) + ": One NationalPark element is missing a Phone element.");
                }

                Match addressMatch = Regex.Match(parkText, @"<Address\b(?![^>]*\bNearestAirport\s*=)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (addressMatch.Success)
                {
                    AddError(errors, "Line " + GetLineNumber(xmlText, parkMatch.Index + addressMatch.Index) + ": One Address element is missing the required NearestAirport attribute.");
                }
            }
        }

        private static int GetLineNumber(string text, int index)
        {
            int line = 1;

            for (int i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                }
            }

            return line;
        }
    }

}
