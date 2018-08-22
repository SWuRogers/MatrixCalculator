using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Console1
{
    class MatrixCalculator
    {
        static HttpClient client;
        static bool acceptCompressedData = false;
        public static async void RunIt()
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip 
            };
            client = new HttpClient(handler);

            if (acceptCompressedData)
            {
                client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            }
            
            int count = 1000;

            count = await InitDataSet(count);

            var time1 = DateTime.Now;
            var lists = await FetchDataSets(count);
            var time2 = DateTime.Now;

            var result = Transform(lists[0], lists[1]);
            var endtime = DateTime.Now;

            Console.WriteLine("Total Time Spent: " + (endtime - time1).TotalSeconds);
            Console.WriteLine("Time Spent on Fetching Data: " + (time2 - time1).TotalSeconds);
            Console.WriteLine("Time Spent on Calculating: " + (endtime - time2).TotalSeconds);

            var phrase = await Validate(result);

            Console.WriteLine(phrase);

            //PrintMatrix(lists[0]);
            //PrintMatrix(lists[1]);
            //PrintMatrix(result);
        }



        private static async Task<string> Validate(int[,] source)
        {
            int count = source.GetLength(0);

            StringBuilder s = new StringBuilder();

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    s.Append(source[i, j]);
                }
            }
            StringBuilder strResult = new StringBuilder();
            using (MD5 md5 = MD5.Create())
            {

                byte[] byteResult = md5.ComputeHash(Encoding.UTF8.GetBytes(s.ToString()));

                for (int i = 0; i < byteResult.Length; i++)
                {
                    //strResult.Append(byteResult[i].ToString("x2"));
                    strResult.Append(byteResult[i].ToString());
                }
            }
            using (var client = new HttpClient())
            {
                var resp = await client.PostAsync("http://numberservice.azurewebsites.net/api/numbers/validate", new StringContent(strResult.ToString(), Encoding.UTF8, "application/json"));
                var result = await resp.Content.ReadAsAsync<Response<string>>();

                return result.Value;
            }
        }

        private static async Task<int> InitDataSet(int mCount)
        {
            using (var client = new HttpClient())
            {
                var uri = string.Format("http://numberservice.azurewebsites.net/api/numbers/init/{0}", mCount);
                var resp = await client.GetAsync(uri);
                if (!resp.IsSuccessStatusCode)
                {
                    throw new Exception("Something wrong while populating data: "+ resp.ReasonPhrase);
                }
                var r = await resp.Content.ReadAsAsync<Response<int>>();
                return r.Value;
            }
        }

        private static async Task<List<int[]>[]> FetchDataSets(int n)
        {
            var lst1 = new List<int[]>();
            var lst2 = new List<int[]>();
            var tasks1 = Enumerable.Range(0, n)
                .Select(row => GetRow(MatrixName.A, row));
            var tasks2 = Enumerable.Range(0, n)
                .Select(row => GetRow(MatrixName.B, row));

            Task t2 = Task.Run(async () =>
            {
                lst2 = (await Task.WhenAll(tasks2)).ToList();
            }
            );

            lst1 = (await Task.WhenAll(tasks1)).ToList();
            t2.Wait();

            return new List<int[]>[]
            {
                        lst1, lst2
            };
        }

        //private static async Task<List<int[]>[]> FetchDataSets(int n)
        //{

        //    var lst1 = new List<int[]>();
        //    var lst2 = new List<int[]>();

        //    var tasks = Enumerable.Range(0, n)
        //        .Select(row => GetRow(MatrixName.A, row));

        //    lst1 = (await Task.WhenAll(tasks)).ToList();

        //    tasks = Enumerable.Range(0, n)
        //        .Select(row => GetRow(MatrixName.B, row));

        //    lst2 = (await Task.WhenAll(tasks)).ToList();

        //    return new List<int[]>[]
        //    {
        //                lst1, lst2
        //    };
        //}

        private static async Task<int[]> GetRow(MatrixName matrixName, int rowNum)
        {


            var getRowUri = string.Format("http://numberservice.azurewebsites.net/api/numbers/{0}/row/{1}", matrixName.ToString(), rowNum);


            
            var resp = await client.GetAsync(getRowUri);
            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception("Something wrong while Gettting Row: " + resp.ReasonPhrase);
            }


            var row = await resp.Content.ReadAsAsync<Response<int[]>>();
            return row.Value;



        }

        private static int[,] Transform(IList<int[]> lst1, IList<int[]> lst2)
        {
            var count = lst1.Count;
            int[,] result = new int[count, count];


            for (int r = 0; r < count; r++)
            {
                for (int c1 = 0; c1 < count; c1++)
                {
                    for (int r1 = 0; r1 < count; r1++)
                    {
                        var currentResult = lst1[r][r1] * lst2[r1][c1];
                        result[r, c1] += currentResult;
                    }
                }
            }
            return result;
        }

        private static void PrintMatrix(IList<int[]> toPrint)
        {
            int count = toPrint.Count();

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    Console.Write(toPrint[i][j] + " ");
                }
                Console.WriteLine();
            }
            Console.WriteLine("======================");
        }

        private static void PrintMatrix(int[,] toPrint)
        {
            int count = toPrint.GetLength(0);

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    Console.Write(toPrint[i, j] + " ");
                }
                Console.WriteLine();
            }
            Console.WriteLine("======================");
        }
    }

    enum MatrixName
    {
        A, B
    }

    public class Response<T>
    {
        public string Cause { get; set; }
        public bool Success { get; set; }
        public T Value { get; set; }
    }
}
