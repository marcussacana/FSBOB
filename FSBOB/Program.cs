using AdvancedBinary;
using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace FSBOB {
    class Program {

        static string ConfigPath { get { return AppDomain.CurrentDomain.BaseDirectory + "FSBOB.bin"; } }
        static void Main(string[] args) {
            Console.Title = "Fast SA Blak Desert Bootstrap - Pelo Marcussacana";
            Console.WriteLine("Inicializando...");
            UpdateCheck();
            Config Cfg = new Config();
        again:;
            if (File.Exists(ConfigPath)) {
                byte[] CfgData = File.ReadAllBytes(ConfigPath);
                Encryption(ref CfgData);
                try {
                    Tools.ReadStruct(CfgData, ref Cfg);
                } catch {
                    File.Delete(ConfigPath);
                    goto again;
                }
            } else {
                Console.WriteLine("Email:");
                Cfg.Username = Encoding.UTF8.GetBytes(Console.ReadLine());
                Console.WriteLine("Senha:");
                Cfg.Password = Encoding.UTF8.GetBytes(SafeInput());
            }

            Login(Cfg.Username, Cfg.Password);
            if (AccToken == null) {
                Console.WriteLine("Autenticação Não Autorizada...");
                if (File.Exists(ConfigPath))
                    File.Delete(ConfigPath);
                goto again;
            }

            Console.WriteLine("Autenticação Autorizada...");

            if (!File.Exists(ConfigPath)) {
                Console.WriteLine("Deseja salvar suas credenciais para as próximas inicializações? S/N");
                bool Save = Console.ReadKey().KeyChar.ToString().ToUpper() == "S";
                Console.WriteLine();
                if (Save) {
                    byte[] Content = Tools.BuildStruct(ref Cfg);
                    Encryption(ref Content);
                    File.WriteAllBytes(ConfigPath, Content);
                    Console.WriteLine("Credenciais salvas...");
                }
            }

            Console.WriteLine("Autenticando...");
            Auth(Cfg.Username);
            
            if (Username == null || UserID == null || Token == null) {
                Console.WriteLine("Falha ao autenticar...");
                Console.WriteLine("Pressione qualquer tecla para sair");
                Console.ReadKey();
                return;
            }
            
            Console.WriteLine("Autenticado, Bem-Vindo {0}... Iniciando o Black Desert...", Username);
            BootGame();

            System.Threading.Thread.Sleep(5000);
        }

        private static void UpdateCheck() {
            int ver = 0;
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "client_version")) {
                string str = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "client_version", Encoding.UTF8);
                str = str.Trim(' ', '\n', '\r');
                ver = int.Parse(str);
            }
            const string ClientTag = "client version:";
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "update.log")) {
                string[] Lines = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "update.log");
                for (int i = Lines.Length - 1; i >= 0; i--) {
                    string Line = Lines[i];
                    if (!Line.Contains(ClientTag))
                        continue;
                    int Pos = Line.IndexOf(ClientTag) + ClientTag.Length;
                    Line = Line.Substring(Pos, Line.Length - Pos).Trim();
                    ver = int.Parse(Line);
                    break;
                }
            }
            if (ver == 0)
                return;
            Console.WriteLine("Procurando por Atualizações do Black Desert...");
            const string UpdatePath = "http://blackdesert.cdn.playredfox.net/BlackDesert/Live/client_version";
            string[] Info = new WebClient().DownloadString(UpdatePath).Split('\n');
            int LastVer = int.Parse(Info[0].Trim());

            if (LastVer > ver) {
                Console.WriteLine("Ops... Atualize o Black Desert antes...");
                string BOL = AppDomain.CurrentDomain.BaseDirectory + "BlackDesertLauncher.exe";
                if (File.Exists(BOL))
                    Process.Start(BOL);
                System.Threading.Thread.Sleep(4000);
                Environment.Exit(0);
            }

            Console.WriteLine("Nenhuma Atualização Encontrada...");
        }

        //userid|username|token
        const string Paramters = "{0}|{1}|{2}";
        private static void BootGame() {
            string EXE = AppDomain.CurrentDomain.BaseDirectory + (Environment.Is64BitOperatingSystem ? "bin64\\BlackDesert64.exe" : "bin\\BlackDesert32.exe");
            Process.Start(new ProcessStartInfo() {
                FileName = EXE,
                Arguments = string.Format(Paramters, UserID, Username, Token),
                WorkingDirectory = Path.GetDirectoryName(EXE)
            });
        }

        static string UserID = null;
        static string Username = null;
        static string Token = null;
        const string AuthAddr = "https://www.playredfox.com/black_desert/launcher/sign_in.json";
        private static void Auth(byte[] User) {
            HttpWebRequest Initialize = WebRequest.CreateHttp(AuthAddr);
            Initialize.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            Initialize.Method = "POST";
            Initialize.CookieContainer = new CookieContainer();
            Cookie[] CookieColl = new Cookie[] {
                new Cookie("_bdo_launcher_email", Encoding.UTF8.GetString(User)),
                new Cookie("SERVERID", Server),
                new Cookie("request_method", Initialize.Method),
                new Cookie("_redfox-web_session", AccToken)
            };

            foreach (Cookie Cookie in CookieColl)
                Initialize.CookieContainer.Add(new Uri(AuthAddr), Cookie);

            MemoryStream RequestData = new MemoryStream(Encoding.UTF8.GetBytes("lang=pt"));
            Stream RequestStrm = Initialize.GetRequestStream();
            RequestData.CopyTo(RequestStrm);
            RequestData.Close();
            RequestStrm.Close();

            HttpWebResponse Response = (HttpWebResponse)Initialize.GetResponse();
            Stream Reply = Response.GetResponseStream();
            MemoryStream ReplyData = new MemoryStream();
            Reply.CopyTo(ReplyData);
            Reply.Close();
            string Content = Encoding.UTF8.GetString(ReplyData.ToArray());
            ReplyData.Close();

            UserID = ReadJson(Content, "user_id");
            Username = ReadJson(Content, "username");
            Token = ReadJson(Content, "user_token");

            if (!Content.Contains(AllowedMsg))
                Username = null;
        }

        static string ReadJson(string JSON, string Name) {
            string Finding = string.Format("\"{0}\":", Name);
            int Pos = JSON.IndexOf(Finding) + Finding.Length;
            if (Pos - Finding.Length == -1)
                return null;

            string Cutted = JSON.Substring(Pos, JSON.Length - Pos).TrimStart(' ', '\n', '\r');
            char Close = Cutted.StartsWith("\"") ? '"' : ',';
            Cutted = Cutted.TrimStart('"');
            string Data = string.Empty;
            foreach (char c in Cutted) {
                if (c == Close)
                    break;
                Data += c;
            }
            if (Data.Contains("\\"))
                throw new Exception("Ops... Unsupported Json Format...");

            return Data;
        }

        static string AccToken = null;
        static string Server = null;
        const string AllowedMsg = "\"result_code\":100";
        const string LoginForm = "email={0}&password={1}&lang=pt";
        const string LoginAddr = "https://www.playredfox.com/black_desert/launcher/purchase_check.json";
        private static void Login(byte[] User, byte[] Pass) {
            string Username = Encoding.UTF8.GetString(User), Password = Encoding.UTF8.GetString(Pass);
            HttpWebRequest Initialize = WebRequest.CreateHttp(LoginAddr);
            Initialize.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            Initialize.Method = "POST";
            string Login = string.Format(LoginForm, WebUtility.UrlEncode(Username), WebUtility.UrlEncode(Password));

            MemoryStream PostData = new MemoryStream(Encoding.UTF8.GetBytes(Login));
            Stream Post = Initialize.GetRequestStream();
            PostData.CopyTo(Post);
            PostData.Close();
            Post.Close();

            HttpWebResponse Response = (HttpWebResponse)Initialize.GetResponse();
            MemoryStream ResponseData = new MemoryStream();
            Stream Read = Response.GetResponseStream();
            Read.CopyTo(ResponseData);
            Read.Close();
            string ServerReply = Encoding.UTF8.GetString(ResponseData.ToArray());
            ResponseData.Close();


            AccToken = string.Empty;
            for (int i = 0; i < Response.Headers.Count; i++) {
                string HName = Response.Headers.GetKey(i);
                string[] Values = Response.Headers.GetValues(i);
                if (HName.ToLower().Trim() != "set-cookie")
                    continue;
                foreach (string Content in Values) {
                    string[] Cnt = Content.Split(';');
                    foreach (string Cookie in Cnt) {
                        if (!Cookie.Contains("="))
                            continue;

                        string Name = Cookie.Split('=')[0].Trim();
                        string Value = Cookie.Split('=')[1].Trim();
                        switch (Name) {
                            case "_redfox-web_session":
                                AccToken = Value;
                                break;
                            case "SERVERID":
                                Console.WriteLine("Servidor Autorizado: {0}", Value);
                                Server = Value;
                                break;
                            default:
#if DEBUG
                                Console.WriteLine("Ignorando Cookie: {0}", Name);
#endif
                                break;
                        }
                    }
                }
            }

            if (!ServerReply.Contains(AllowedMsg))
                AccToken = null;
        }

        struct Config {

            [PArray(PrefixType = Const.UINT32)]
            public byte[] Username;

            [PArray(PrefixType = Const.UINT32)]
            public byte[] Password;
            
        }


        private static string SafeInput() {
            int StartTop = Console.CursorTop;
            string Input = string.Empty;
            while (true) {
                ConsoleKeyInfo Key = Console.ReadKey(true);
                if (Key.Key == ConsoleKey.Backspace) {
                    if (Input.Length == 0)
                        continue;
                    Input = Input.Substring(0, Input.Length - 1);
                    if (Console.CursorLeft - 1 < 0 && Console.CursorTop > StartTop) {
                        Console.CursorTop = StartTop;
                        for (int i = 0; i < Input.Length + 1; i++)
                            Console.Write(" ");
                        Console.CursorLeft = 0;
                        Console.CursorTop = StartTop;
                        for (int i = 0; i < Input.Length; i++)
                            Console.Write("*");
                        continue;
                    }
                    Console.CursorLeft--;
                    Console.Write(" ");
                    Console.CursorLeft--;
                } else if (Key.Key == ConsoleKey.Enter) {
                    break;
                } else {
                    Input += Key.KeyChar;
                    Console.Write("*");
                }
            }
            Console.WriteLine();
            return Input;
        }
        private static void DownloadProgress(string URL, string SaveAs) {
            DownLen = 0;
            Downloaded = 0;
            new System.Threading.Thread(() => { Download(URL, SaveAs); }).Start();
            while (DownLen == 0)
                System.Threading.Thread.Sleep(500);
            if (DownLen == -1) {
                while (Downloaded != -1)
                    System.Threading.Thread.Sleep(500);
                return;
            } else {
                SpecialColor(true, ConsoleColor.Green);
                Console.WriteLine();
                int RealLen = 0;
                int Len = Console.WindowWidth - 7;
                if (Len > 100)
                    Len = 100;
                double BB = 100 / (double)Len;
                while (Downloaded != -1) {
                    int TProg = (int)(((double)Downloaded / DownLen) * 100);
                    Console.CursorTop--;
                    Console.CursorLeft = 0;
                    Console.Write("[");
                    double writed = 0, prog = TProg / BB;
                    RealLen = 0;
                    while (writed < Len) {
                        Console.Write(writed < prog ? "=" : " ");
                        writed += BB;
                        RealLen += 1;
                    }
                    Console.WriteLine("] {0}%", TProg);
                    System.Threading.Thread.Sleep(500);
                }
                Console.CursorTop--;
                Console.CursorLeft = 0;
                Console.Write("[");
                int tmp = 0;
                while (tmp++ < RealLen)
                    Console.Write("=");
                Console.WriteLine("] 100%");
                SpecialColor(false);
            }
        }
        private static long DownLen = 0;
        private static long Downloaded = 0;
        private static void Download(string URL, string SaveAs) {
            try {
                HttpWebRequest Request = (HttpWebRequest)WebRequest.Create(URL);
                Request.UseDefaultCredentials = true;
                Request.Method = "GET";
                WebResponse Response = Request.GetResponse();
                if (File.Exists(SaveAs))
                    File.Delete(SaveAs);
                bool know = long.TryParse(Response.Headers["Content-Length"], out DownLen);
                if (!know) {
                    DownLen = -1;
                    Downloaded = 0;
                }
                using (FileStream Data = new FileStream(SaveAs, FileMode.CreateNew))
                using (Stream Reader = Response.GetResponseStream()) {
                    byte[] Buffer = new byte[1024 * 20];
                    int bytesRead = 0;
                    do {
                        bytesRead = Reader.Read(Buffer, 0, Buffer.Length);
                        if (know)
                            Downloaded += bytesRead;
                        Data.Write(Buffer, 0, bytesRead);
                    } while (bytesRead > 0);
                }
            } catch {

            }
            Downloaded = -1;
        }
        private static ConsoleColor b = Console.ForegroundColor;
        private static void SpecialColor(bool Enable, ConsoleColor Color = ConsoleColor.Red) => Console.ForegroundColor = Enable ? Color : b;

        //Proteção para caso você acabe fazendo "upload" dos dados de suas credênciais...
        static void Encryption(ref byte[] Array) {
            ulong Key = GetKey();
            ulong NxtKey = Key ^ 0xFFFFFFFFFFFFFF; //Insira um valor aleatório aqui para ter certeza de que está seguro
            ulong BckKey = 0;


            for (int i = 0; i < Array.Length; i++) {
                Key ^= ((NxtKey << 8) ^ (BckKey << 8));
                NxtKey = (Key ^ (Key >> 8 * 4)) | BckKey << (8 * 4) * 2;
                BckKey ^= Key ^ NxtKey;
                Key >>= 8;
                if (Key < 0xFF)
                    Key = NxtKey ^ 0xABD1C2F395C;

                Array[i] ^= (byte)(Key & 0xFF);
            }
        }

        private static ulong GetKey() {
            byte[] Seed = Encoding.UTF8.GetBytes(Environment.UserName + Environment.OSVersion);

            if (Seed.Length < 4)
                return 0x90F9BC1CF91;

            ulong Key = (ulong)(Seed[0] | (Seed[1] << 8 * 1) | (Seed[2] << 8 * 2) | (Seed[3] << 8 * 3));
            Key |= Key << (8 * 3) * 2;
            for (int i = 0, l = 0; i < Seed.Length; l = Seed[i++]) {
                Key ^= (ulong)(Seed[i % (Seed.Length - 1)] ^ Seed[i]);
            }

            return Key ^ 0xFFFFFFFFFFFFFF;
        }
    }
}