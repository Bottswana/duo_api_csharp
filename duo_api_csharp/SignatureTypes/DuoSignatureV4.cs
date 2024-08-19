/*
 * Copyright (c) 2022-2024 Cisco Systems, Inc. and/or its affiliates
 * All rights reserved
 * https://github.com/duosecurity/duo_api_csharp
 */

using System.Web;
using System.Text;
using Newtonsoft.Json;
using duo_api_csharp.Models;
using duo_api_csharp.Extensions;
using System.Security.Cryptography;

namespace duo_api_csharp.SignatureTypes
{
    public class DuoSignatureV4(string ikey, string skey, string host, DateTime requesttime) : IDuoSignatureTypes
    {
        public DuoSignatureTypes SignatureType => DuoSignatureTypes.Duo_SignatureTypeV4;
        public Dictionary<string, string> DefaultRequestHeaders => new()
        {
            { "X-Duo-Date", requesttime.DateToRFC822() }
        };

        public string SignRequest(HttpMethod method, string path, DateTime requestDate, DuoRequestData? requestData, Dictionary<string, string>? requestHeaders)
        {
            // Format data for signature
            var signingHeader = $"{requestDate.DateToRFC822()}\n{method.Method.ToUpper()}\n{host}\n{path}";
            var signingParams = new StringBuilder();
            var bodyData = "";
            
            // Check request data for signing
            if( requestData is DuoParamRequestData paramData )
            {
                foreach( var (paramKey, paramValue) in paramData.RequestData )
                {
                    if( signingParams.Length != 0 ) signingParams.Append('&');
                    signingParams.Append($"{HttpUtility.UrlEncode(paramKey)}={HttpUtility.UrlEncode(paramValue)}");
                }
            }
            else if( requestData is DuoJsonRequestData jsonData )
            {
                bodyData = jsonData.RequestData;
            }
            else if( requestData is DuoJsonRequestDataObject { RequestData: not null } jsonDataWithObject )
            {
                var jsonFormattingSettings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                };
                
                bodyData = JsonConvert.SerializeObject(jsonDataWithObject.RequestData, jsonFormattingSettings);
            }
            
            //Console.WriteLine($"---------");
            //Console.WriteLine($"{signingHeader}\n{signingParams}\n{_Sha512Hash(bodyData)}\n{_Sha512Hash(signingHeaders.ToString())}");
            //Console.WriteLine($"---------\n{signingHeaders}");

            // Return HMAC signature for request
            var auth = $"{ikey}:{_HmacSign($"{signingHeader}\n{signingParams}\n{_Sha512Hash(bodyData)}")}";
            return _Encode64(auth);
        }
        
        private string? _HmacSign(string data)
        {
            var key_bytes = Encoding.UTF8.GetBytes(skey);
            var hmac = new HMACSHA512(key_bytes);

            var data_bytes = Encoding.UTF8.GetBytes(data);
            hmac.ComputeHash(data_bytes);
            if( hmac.Hash == null ) return null;

            var hex = BitConverter.ToString(hmac.Hash);
            return hex.Replace("-", "").ToLower();
        }
        
        private string _Sha512Hash(string data)
        {
            var data_bytes = Encoding.UTF8.GetBytes(data);
            var hash_data = SHA512.HashData(data_bytes);
            var hex = BitConverter.ToString(hash_data);
            return hex.Replace("-", "").ToLower();
        }

        private string _Encode64(string plaintext)
        {
            var plaintext_bytes = Encoding.UTF8.GetBytes(plaintext);
            return Convert.ToBase64String(plaintext_bytes);
        }
    }
}