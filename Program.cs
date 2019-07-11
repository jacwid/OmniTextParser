using Accord.IO;
using AngleSharp;
using AngleSharp.Dom;
using CsvHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OmniTextParser
{
    public class Program
    {
        static readonly HttpClient client = new HttpClient();
        static readonly string path = "C:\\dev\\AI\\OmniTextParser\\";

        static Dictionary<string, int> VanligasteOrden { get; set; }
        static Dictionary<string, int> VanligasteTaggarna { get; set; }
        static Dictionary<string, int> OrdStringDict { get; set; }
        static Dictionary<int, string> OrdIntDict { get; set; }
        static Dictionary<string, int> KategoriStringDict { get; set; }
        static Dictionary<int, string> KategoriIntDict { get; set; }

        static async Task Main(string[] args)
        {
            VanligasteOrden = new Dictionary<string, int>();
            VanligasteTaggarna = new Dictionary<string, int>();
            OrdStringDict = new Dictionary<string, int>();
            OrdIntDict = new Dictionary<int, string>();
            KategoriStringDict = new Dictionary<string, int>();
            KategoriIntDict = new Dictionary<int, string>();

            string url;
            if (args.Length == 1)
            {
                url = args[0];
            }
            else
            {
                url = "https://omni-content.omni.news/articles?limit=2000&sort=latest";
            }

            var artiklar = await JsonHandler(url);

            artiklar = RensaArtiklar(artiklar);

            SkapaDictionaries(artiklar);

            SkapaIntCSV(artiklar, path + "omni_data_int.csv");

            SkapaNpzFil(artiklar, path);
        }

        static void SkapaNpzFil(List<OmniArtikel> artiklar, string path)
        {
            using (var stream = File.OpenWrite(path + "x_train.npy"))
            {
                var arrayList = new List<int[]>();

                foreach (var artikel in artiklar)
                {
                    arrayList.Add(TextTillIndexArray(artikel.Text.Trim()));
                }

                var yttreArray = arrayList.ToArray();

                NpyFormat.Save(arrayList[0], stream);
            }

            using (var stream2 = File.OpenWrite(path + "x_test.npy"))
            {
                var arrayList = new List<int[]>();

                foreach (var artikel in artiklar)
                {
                    arrayList.Add(TextTillIndexArray(artikel.Text.Trim()));
                }

                var yttreArray = arrayList.ToArray();

                NpyFormat.Save(arrayList[1], stream2);
            }

            var npyFiler = new List<string>();
            npyFiler.Add(path + "x_train.npy");
            npyFiler.Add(path + "x_test.npy");

            var arrayList2 = new List<int[]>();

            foreach (var artikel in artiklar)
            {
                arrayList2.Add(TextTillIndexArray(artikel.Text.Trim()));
            }

            var dict = new Dictionary<string, Array>();
            dict.Add("x_train.npy", arrayList2[0]);
            dict.Add("x_test.npy", arrayList2[1]);

            using (var stream3 = File.OpenWrite(path + "omni_data.npz"))
            {
                NpzFormat.Save(dict, stream3, System.IO.Compression.CompressionLevel.Fastest);
            }
        }

        static void SkapaIntCSV(List<OmniArtikel> artiklar, string path)
        {
            using (var stream = File.OpenWrite(path))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            using (var csvWriter = new CsvHelper.CsvWriter(writer))
            {
                csvWriter.Configuration.Delimiter = ",";
                csvWriter.Configuration.HasHeaderRecord = false;

                // Headers
                csvWriter.WriteField("x");
                csvWriter.WriteField("y");
                csvWriter.NextRecord();

                foreach (var artikel in artiklar)
                {
                    csvWriter.WriteField(TextTillIndexArray(artikel.Text.Trim()));
                    csvWriter.WriteField(KategorierTillIndexArray(artikel.Taggar.Select(t => t.TopicId).ToArray()));
                    csvWriter.NextRecord();
                }
            }
        }

        static void SkapaDictionaries(List<OmniArtikel> artiklar)
        {
            foreach (var artikel in artiklar)
            {
                // Räkna förekomsterna av olika ord för att se vilka som är vanligast
                var artikelOrd = AllaOrdIText(artikel.Text);
                foreach (var ord in artikelOrd)
                {
                    if (!VanligasteOrden.ContainsKey(ord))
                        VanligasteOrden.Add(ord, 0);

                    VanligasteOrden[ord] += 1;
                }


                // Räkna förekomsterna av olika taggar för att se vilka som är vanligast
                foreach (var tag in artikel.Taggar)
                {
                    if (!VanligasteTaggarna.ContainsKey(tag.TopicId))
                        VanligasteTaggarna.Add(tag.TopicId, 0);

                    VanligasteTaggarna[tag.TopicId] += 1;
                }
            }


            // Ge alla ord ett index, ju vanligare ord desto lägre index
            var ordIndex = 0;
            foreach (var kvp in VanligasteOrden.OrderByDescending(kvp => kvp.Value))
            {
                OrdStringDict.Add(kvp.Key, ordIndex);
                OrdIntDict.Add(ordIndex, kvp.Key);
                ordIndex++;
            }


            // Ge alla kategorier ett index, ju vanligare ord desto lägre index
            var kategoriIndex = 0;
            foreach (var kvp in VanligasteTaggarna.OrderByDescending(kvp => kvp.Value))
            {
                KategoriStringDict.Add(kvp.Key, kategoriIndex);
                KategoriIntDict.Add(kategoriIndex, kvp.Key);
                kategoriIndex++;
            }


            // Skriv dictonaries som json till fil
            using (StreamWriter file = File.CreateText(path + "omni_word_index_string.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, OrdStringDict);
            }

            using (StreamWriter file = File.CreateText(path + "omni_word_index_int.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, OrdIntDict);
            }

            using (StreamWriter file = File.CreateText(path + "omni_category_index_string.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, KategoriStringDict);
            }

            using (StreamWriter file = File.CreateText(path + "omni_category_index_int.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, KategoriIntDict);
            }
        }

        static int[] KategorierTillIndexArray(string[] kategoriIdn)
        {
            List<int> index = new List<int>();
            index.Add(777);
            foreach (var kategoriId in kategoriIdn)
            {
                index.Add(KategoriStringDict[kategoriId]);
            }

            return index.ToArray();
        }

        static int[] TextTillIndexArray(string text)
        {
            List<int> index = new List<int>();
            index.Add(555);
            var allaOrd = AllaOrdIText(text);

            foreach (var ord in allaOrd)
            {
                index.Add(OrdStringDict[ord]);
            }

            return index.ToArray();
        }

        static string[] AllaOrdIText(string text)
        {
            List<string> allaOrd = new List<string>();

            // Splitta på mellanslag
            var allaOrdITexten = text.Trim().ToLower().Split(" ");

            // Ta bort whitespace från ord
            foreach (var ordITexten in allaOrdITexten)
            {
                var ord = new string(ordITexten.Where(c => !char.IsWhiteSpace(c)).ToArray());

                if (!string.IsNullOrEmpty(ord))
                    allaOrd.Add(ord);
            }

            return allaOrd.ToArray();
        }

        static List<OmniArtikel> RensaArtiklar(List<OmniArtikel> artiklar)
        {
            var retval = new List<OmniArtikel>();

            foreach (var artikel in artiklar)
            {
                if (string.IsNullOrEmpty(artikel.Text.Trim()))
                    continue;

                if (artikel.Taggar.Count() == 0)
                    continue;

                retval.Add(artikel);
            }

            return retval;
        }

        /// <summary>
        /// Läser in texter från OMNI
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        static async Task<List<OmniArtikel>> JsonHandler(string url)
        {
            var client = new HttpClient();
            var result = await client.GetStringAsync(url);

            dynamic document = JObject.Parse(result);

            var retval = new List<OmniArtikel>();

            foreach (var articleOuterObject in document.articles)
            {
                var article = articleOuterObject.First;

                if (article == null || article.title == null)
                    continue;

                string articleTitle = article.title.value;

                var taggar = new List<OmniTag>();

                string articleText = "";
                foreach (var paragraph in article.main_text.paragraphs)
                {
                    articleText = string.Join(" ", articleText, paragraph.text.value);
                }

                foreach (var tag in article.tags)
                {
                    string tagTitle = tag.title;

                    taggar.Add(new OmniTag()
                    {
                        TopicId = tag.topic_id,
                        Titel = tagTitle.Trim(),
                        Typ = tag.type
                    });
                }

                retval.Add(new OmniArtikel()
                {
                    Id = article.article_id,
                    Titel = articleTitle.ToString().Trim(),
                    Text = articleText.Trim(),
                    Taggar = taggar
                });
            }

            return retval;
        }

        private class OmniArtikel
        {
            public string Id { get; set; }            
            public string Titel { get; set; }
            public string Text { get; set; }
            public List<OmniTag> Taggar { get; set; }
        }

        private class OmniTag
        {
            [JsonProperty("topic_id")]
            public string TopicId { get; set; }
            [JsonProperty("title")]
            public string Titel { get; set; }
            [JsonProperty("type")]
            public string Typ { get; set; }
        }
    }
}
