using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Stormancer.Server;

namespace Server.Database
{
    class AuthenticatedHttpClientHandler : HttpClientHandler
    {
        private Index _index;
        public AuthenticatedHttpClientHandler(Index index)
        {
            _index = index;

        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            request.Headers.Add("x-token", GenerateToken());
            request.Headers.Add("x-version", "1.0.0");
            return base.SendAsync(request, cancellationToken);
        }

        private string GenerateToken()
        {

            return CreateToken(new ApiToken { Issued = DateTime.UtcNow, Expiration = DateTime.UtcNow.AddSeconds(1200) }, _index.primaryKey);

        }


        private static string CreateToken<T>(T data, string key)
        {

            var str = Base64Encode(JsonConvert.SerializeObject(data));
            return string.Format("{0}.{1}", str, ComputeSignature(str, key));
        }

        private static string ComputeSignature(string data, string key)
        {
            using (var sha = SHA256CryptoServiceProvider.Create())
            {
                sha.Initialize();
                var bytes = System.Text.Encoding.UTF8.GetBytes(data + key);
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        private static string Base64Encode(string data)
        {
            try
            {
                var byte_data = System.Text.Encoding.UTF8.GetBytes(data);
                string encodedData = Convert.ToBase64String(byte_data);
                return encodedData;
            }
            catch (Exception e)
            {
                throw new Exception("An error occured during base 64 encoding.", e);
            }
        }
        internal class ApiToken
        {
            public ApiToken()
            {

            }

            public DateTime Expiration
            {
                get;
                set;
            }

            public DateTime Issued
            {
                get;
                set;
            }

        }
    }
}
