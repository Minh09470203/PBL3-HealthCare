using Microsoft.Extensions.Configuration;
using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PBL3_HealthCare.Services
{
    public class ZegoTokenService
    {

        private readonly long _appId = 321894638;
        private readonly string _serverSecret = "49bb089a9ed9590dd5a3a7e0b638ae9f";

        public string GenerateToken(string userId, string roomId, int role)
        {
            return $"token_for_{userId}_in_{roomId}";
        }
    }
}