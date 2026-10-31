using System;
using System.IO;
using System.Net;
using System.Text;
using System.Security.Cryptography;

public static partial class RegressionTests {
    static byte[] DerField(byte tag, params byte[][] parts) {
        using (var stream = new MemoryStream()) {
            int length = 0; foreach (byte[] part in parts) length += part.Length;
            stream.WriteByte(tag);
            if (length < 128) stream.WriteByte((byte)length);
            else { stream.WriteByte(0x82); stream.WriteByte((byte)(length >> 8)); stream.WriteByte((byte)length); }
            foreach (byte[] part in parts) stream.Write(part, 0, part.Length);
            return stream.ToArray();
        }
    }
    static void TestRestart() {
        using (var rsa = new RSACryptoServiceProvider(1024)) {
            rsa.PersistKeyInCsp = false;
            var key = rsa.ExportParameters(false);
            byte[] rsaDer = DerField(0x30, DerField(0x02, new byte[] { 0 }, key.Modulus), DerField(0x02, key.Exponent));
            byte[] algorithm = new byte[] { 0x30, 0x0d, 0x06, 0x09, 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x0d, 0x01, 0x01, 0x01, 0x05, 0x00 };
            string pem = "-----BEGIN PUBLIC KEY-----\\n" + Convert.ToBase64String(DerField(0x30, algorithm, DerField(0x03, new byte[] { 0 }, rsaDer))) + "\\n-----END PUBLIC KEY-----";
            string page = "<input id=\"Frm_Username\" value=\"test\"><input id=\"Frm_Password\" value=\"test\"><script>var pubKey = \"" + pem + "\";</script>";
            string view = "_sessionTmpToken = \"\\x41\\x26\\x42\";";
            var command = RestartRequest.Create(view, page);
            Assert(command.Body == "IF_ACTION=Restart&_sessionTOKEN=A%26B", "restart token decoded and escaped");
            string digest;
            using (var sha = SHA256.Create()) digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(command.Body))).Replace("-", "").ToLowerInvariant();
            Assert(Encoding.UTF8.GetString(rsa.Decrypt(Convert.FromBase64String(command.Check), false)) == digest, "restart integrity header matches exact body");
            foreach (string outcome in new string[] { "SUCC", "rejected", "drop", "malformed", "missing" }) {
                int posts = 0;
                Func<string,string,string> transport = delegate(string path, string body) {
                    Assert(!path.Contains("db_reset"), "never call factory reset");
                    if (path == "") return page;
                    if (path.Contains("login_token")) return "<token>test</token>";
                    if (path.Contains("login_entry")) return body == null ? "{\"sess_token\":\"test\"}" : "{\"login_need_refresh\":true}";
                    if (path.Contains("rebootAndReset")) return view;
                    if (path == RestartRequest.Path) {
                        posts++; Assert(body == command.Body, "only restart action sent");
                        if (outcome == "drop") throw new WebException("simulated disconnect");
                        if (outcome == "malformed") return "<";
                        if (outcome == "missing") return "<root/>";
                        return "<root><IF_ERRORSTR>" + outcome + "</IF_ERRORSTR></root>";
                    }
                    return "";
                };
                var reader = new RouterReader(new AppSettings { RouterEnabled = true }, transport);
                reader.RequestObserver = delegate(string path, string body, string check) {
                    if (path == RestartRequest.Path) Assert(!String.IsNullOrEmpty(check), "restart carries integrity header");
                };
                bool rejected = false, accepted = false;
                try { accepted = reader.Restart(); } catch (IOException) { rejected = true; }
                Assert(posts == 1, "restart is never retried after " + outcome);
                Assert(rejected == (outcome == "rejected"), "explicit rejection distinct from lost response");
                Assert(accepted == (outcome == "SUCC"), "only SUCC confirms acceptance");
            }
            bool disabled = false;
            try { new RouterReader(new AppSettings(), delegate { throw new Exception("Unexpected request"); }).Restart(); }
            catch (IOException) { disabled = true; }
            Assert(disabled, "disabled router cannot restart");
            bool unsupported = false;
            try { RestartRequest.Create("", page); } catch (IOException) { unsupported = true; }
            Assert(unsupported, "unsupported firmware fails before POST");
        }
    }
}
