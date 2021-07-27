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
            //Объявление всех переменных
            WebClient client = new WebClient();
            string data="";
            Regex regex = new Regex(@"\<img.+?src=\""(?<imgsrc>.+?)\"".+?\>", RegexOptions.ExplicitCapture);
            Regex regexImg = new Regex(@"\/(?<name>[^\/.]+?\.\w+$)", RegexOptions.ExplicitCapture);
            string directory = "Images";
            List<JsonResponse> jsonResponse = new List<JsonResponse>();
            string host;
            string path;
            string savePath;
            JsonResponse tempCase;
            FileInfo fileInfo;
            var endTime = DateTime.Now.AddMinutes(1);
            //Проверка валидности параметра потоков
            if (threadCount > Environment.ProcessorCount)
                threadCount = Environment.ProcessorCount;
            if (threadCount <= 0)
                threadCount = Environment.ProcessorCount;
            //Создание папки для хранения фотографий
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            //Основная логика
            try
            {
                //Создаём переменную для того чтобы выделить хост
                var uri = new UriBuilder(url);
                //Скачиваем html страницу в текстовом виде
                using (Stream data1 = client.OpenRead(url))

                {
                    using (StreamReader reader = new StreamReader(data1))
                    {
                        data = reader.ReadToEnd();
                    }
                }
                //Парсим страницу на нахождение img тэгов
                MatchCollection matches = regex.Matches(data);
                var imagesUrl = matches
                    .Cast<Match>()
                    .Select(m => m.Groups["imgsrc"].Value).ToList();
                //Проверяем на валидность параметр количества картинок
                if (imageCount > imagesUrl.Count)
                    imageCount = imagesUrl.Count;
                if (imageCount <= 0)
                    imageCount = imagesUrl.Count;
                //Оптимизируем количество потоков
                threadCount = (imageCount / 10 + threadCount) / 2;
                if (threadCount < 1)
                    threadCount = 1;
                if (imageCount < threadCount)
                    threadCount = imageCount;
                //Логика скачивания 
                imagesUrl.GetRange(0, imageCount).AsParallel()
                    .WithDegreeOfParallelism(threadCount)
                    .ForAll(value =>
                {
                    //Проверям не прошла ли одна минута
                    if(DateTime.Now<=endTime)
                    {
                        //Проверям нашлось ли имя с помощью регулярки
                        Match match = regexImg.Match(value);
                        if (match.Success)
                        {
                            path = value;
                            string name = match.Groups["name"].Value;
                            //Находим имя и тэг нашей фотографии
                            if (path[0] == '/')
                            {
                                path = "https://" + uri.Host + path;
                                host = uri.Host;

                            }

                            else
                                host = new UriBuilder(value).Host;
                            //Создаём путь хранения фотографии
                            savePath = Path.Combine(directory, name);
                            //Скачиваем фотографию
                            using (WebClient localClient = new WebClient())
                            {
                                localClient.DownloadFile(path, savePath);
                            }
                            //Добавляем информацию в класс который потом будем преобразовывать в json
                            if (jsonResponse.Any(x => x.Host == host))
                            {
                                fileInfo = new FileInfo(savePath);
                                tempCase = jsonResponse.Where(x => x.Host == host).ElementAt(0);
                                tempCase.Images = tempCase.Images.Concat(new ImageInfo[] { new ImageInfo { Alt = value, Src = name, Size = fileInfo.Length } }).ToArray();
                            }

                            else
                            {
                                fileInfo = new FileInfo(savePath);
                                tempCase = new JsonResponse { Host = host };
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
