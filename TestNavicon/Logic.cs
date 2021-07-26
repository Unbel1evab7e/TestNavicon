using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.Threading;

namespace TestNavicon
{
    public class Logic
    {
        
     
        public async Task<string> GetImages(string url, int threadCount, int imageCount)
        {
            WebClient client = new WebClient();
            string data="";
            Regex regex = new Regex(@"\<img.+?src=\""(?<imgsrc>.+?)\"".+?\>", RegexOptions.ExplicitCapture);
            Regex regexImg = new Regex(@"\/(?<name>[^\/.]+?\.\w+$)", RegexOptions.ExplicitCapture);
            string directory = "Images";
            List<JsonResponse> jsonResponse = new List<JsonResponse>();
            string host;
            FileInfo fileInfo;
            var endTime = DateTime.Now.AddMinutes(1);

            if (threadCount > Environment.ProcessorCount)
                threadCount = Environment.ProcessorCount;
            if (threadCount <= 0)
                threadCount = Environment.ProcessorCount;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            try
            {
                var uri = new UriBuilder(url);

                using (Stream data1 = client.OpenRead(url))

                {
                    using (StreamReader reader = new StreamReader(data1))
                    {
                        data = reader.ReadToEnd();
                    }
                }

                MatchCollection matches = regex.Matches(data);
                var imagesUrl = matches
                    .Cast<Match>()
                    .Select(m => m.Groups["imgsrc"].Value).ToList();

                if (imageCount > imagesUrl.Count)
                    imageCount = imagesUrl.Count;
                if (imageCount <= 0)
                    imageCount = imagesUrl.Count;
                
                threadCount = (imageCount / 10 + threadCount) / 2;
                if (threadCount < 1)
                    threadCount = 1;
                if (imageCount < threadCount)
                    threadCount = imageCount;
                imagesUrl.GetRange(0, imageCount).AsParallel()
                    .WithDegreeOfParallelism(threadCount)
                    .ForAll(value =>
                {
                    if(DateTime.Now<=endTime)
                    {
                        

                        Match match = regexImg.Match(value);
                        if (match.Success)
                        {
                            var path = value;
                            string name = match.Groups["name"].Value;

                            if (path[0] == '/')
                            {
                                path = "https://" + uri.Host + path;
                                host = uri.Host;

                            }
                            else
                                host = new UriBuilder(value).Host;
                            string savePath = Path.Combine(directory, name);
                            using (WebClient localClient = new WebClient())
                            {
                                localClient.DownloadFile(path, savePath);
                            }

                            if (jsonResponse.Any(x => x.Host == host))
                            {
                                fileInfo = new FileInfo(savePath);
                                var tempCase = jsonResponse.Where(x => x.Host == host).ElementAt(0);
                                tempCase.Images = tempCase.Images.Concat(new ImageInfo[] { new ImageInfo { Alt = value, Src = name, Size = fileInfo.Length } }).ToArray();
                            }
                            else
                            {
                                fileInfo = new FileInfo(savePath);
                                var tempCase = new JsonResponse { Host = host };
                                tempCase.Images = new ImageInfo[] { new ImageInfo { Alt = value, Src = name, Size = fileInfo.Length } };

                                jsonResponse.Add(tempCase);
                            }
                        }
                    }
                });

            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                
                using (FileStream fs = new FileStream("ErrorLog.txt", FileMode.Append))
                {
                    byte[] array = Encoding.Default.GetBytes($"\n{DateTime.Now} : {ex.Message}");
                    fs.Write(array, 0, array.Length);
                    
                }
            }
          
            return JsonSerializer.Serialize(jsonResponse.ToArray());
        }
    }
}
