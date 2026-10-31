using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

// Builds the ZTE panel's signed request. Contains no factory-reset operation.
internal sealed class RestartRequest {
    internal const string Path = "?_type=menuData&_tag=devmgr_restartmgr_lua.lua";
    internal string Body, Check;
    internal static RestartRequest Create(string view, string loginPage) {
        var token = Regex.Match(view, "_sessionTmpToken\\s*=\\s*\"([^\"]+)\"");
        var key = Regex.Match(loginPage, "var\\s+pubKey\\s*=\\s*\"([^\"]+)\"");
        if (!token.Success || !key.Success) throw new IOException(L.T("El firmware no permite preparar el reinicio."));
        string body = "IF_ACTION=Restart&_sessionTOKEN=" + Uri.EscapeDataString(Regex.Unescape(token.Groups[1].Value));
        string pem = Regex.Unescape(key.Groups[1].Value);
        byte[] der = Convert.FromBase64String(pem.Replace("-----BEGIN PUBLIC KEY-----", "").Replace("-----END PUBLIC KEY-----", ""));
        var root = new Der(der).Read(0x30);
        root.Read(0x30); // AlgorithmIdentifier
        var bits = root.Read(0x03);
        if (bits.Byte() != 0) throw new IOException("Invalid RSA public key.");
        var rsaKey = bits.Read(0x30);
        byte[] modulus = rsaKey.Read(0x02).Integer(), exponent = rsaKey.Read(0x02).Integer();
        string digest;
        using (var sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(body))).Replace("-", "").ToLowerInvariant();
        using (var rsa = new RSACryptoServiceProvider()) {
            rsa.PersistKeyInCsp = false;
            rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
            return new RestartRequest { Body = body, Check = Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(digest), false)) };
        }
    }
    // Minimal bounded DER reader for SubjectPublicKeyInfo (RSA).
    sealed class Der {
        readonly byte[] data;
        int offset;
        internal Der(byte[] bytes) { data = bytes; }
        internal byte Byte() { if (offset >= data.Length) throw new IOException("Invalid RSA public key."); return data[offset++]; }
        internal Der Read(byte tag) {
            if (Byte() != tag) throw new IOException("Invalid RSA public key.");
            int length = Byte();
            if (length >= 128) {
                int count = length & 127;
                if (count == 0 || count > 4) throw new IOException("Invalid RSA public key.");
                length = 0;
                for (int i = 0; i < count; i++) length = checked(length * 256 + Byte());
            }
            if (length < 0 || length > data.Length - offset) throw new IOException("Invalid RSA public key.");
            var value = new byte[length]; Buffer.BlockCopy(data, offset, value, 0, length); offset += length;
            return new Der(value);
        }
        internal byte[] Integer() {
            if (data.Length == 0) throw new IOException("Invalid RSA public key.");
            int skip = data.Length > 1 && data[0] == 0 ? 1 : 0;
            var value = new byte[data.Length - skip]; Buffer.BlockCopy(data, skip, value, 0, value.Length); return value;
        }
    }
}
