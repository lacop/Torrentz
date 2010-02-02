using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Torrentz
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            //TODO: error reporting

            /*
             * Download torrentz.com page
             */
            string url = "";

            if (args.Length > 0)
            {
                url = args[0];
            }
            else if (Clipboard.GetText() != "")
            {
                url = Clipboard.GetText();
            }
            else
            {
                Console.Write("Enter url: ");
                url = Console.ReadLine();
            }

            if (!url.StartsWith("http://torrentz.com/") && !url.StartsWith("http://torrentz.eu/"))
            {
                Console.WriteLine("URL must start with http://torrentz.com or .eu");
                Console.ReadKey();
                return;
            }

            InfoStarting("Downloading torrentz.com page");
            string torrentzContents = DownloadPageText(url);
            InfoDone();
            
            /*
             * Get torrent file
             */
            InfoStarting("Downloading original .torrent");

            Regex sitesRegex = new Regex("<dl><dt><a href=\"([^\"]+)\" rel=\"e\">", RegexOptions.IgnoreCase);
            byte[] torrent = null;
            foreach (Match match in sitesRegex.Matches(torrentzContents))
            {
                if ((torrent = GetTorrentFromSite(match.Groups[1].Value)) != null)
                    break;
            }

            InfoDone();

            /*
             * Get extra trackers
             */
            InfoStarting("Searching for extra trackers");

            Regex trackersRegex = new Regex("<a href=\"/tracker_[^\"]+\">([^<]+)</a>", RegexOptions.IgnoreCase);
            List<string> trackers = new List<string>();
            foreach (Match match in trackersRegex.Matches(torrentzContents))
            {
                trackers.Add(match.Groups[1].Value);
            }

            InfoDone(string.Format("Found {0} trackers", trackers.Count));

            /*
             * Add new trackers to .torrent file
             */
            InfoStarting("Adding trackers to .torrent");
            
            // Find beginning of announce-list items
            int pos;
            for (pos = 0; pos < torrent.Length; pos++)
            {
                const string needle = "announce-list";
                bool found = true;
                
                for (int i = 0; i < needle.Length && found; i++)
                {
                    if (torrent[pos+i] != (byte)needle[i])
                        found = false;
                }

                if (found)
                {
                    pos += needle.Length;
                    break;
                }
            }
            pos++; // Move to sublist beginning
            
            
            int infile = 0;
            int duplicates = 0;

            while (torrent[pos] == 'l')
            {
                //TODO: support for multiple trackers in one sublist
                pos++;

                // Read length
                int colon = Array.IndexOf(torrent, (byte)':', pos);
                
                string buffer = "";
                for (int i = pos; i < colon; i++) { buffer += (char)torrent[i]; }
                
                int length = Int32.Parse(buffer);

                string tracker = "";
                for (int i = colon + 1; i < colon + length; i++) { tracker += (char)torrent[i]; }

                
                if (trackers.Remove(tracker)) // Avoid duplicates
                    duplicates++;
                
                infile++;

                pos = colon + length + 2; // Move to next sublist or end of list

            }

            StringBuilder extraTrackers = new StringBuilder();
            foreach (string tracker in trackers)
            {
                extraTrackers.AppendFormat("l{0}:{1}e", tracker.Length, tracker);
            }

            byte[] extraBytes = Encoding.ASCII.GetBytes(extraTrackers.ToString());

            byte[] output = new byte[torrent.Length+extraBytes.Length];
            Array.Copy(torrent, 0, output, 0, pos);
            Array.Copy(extraBytes, 0, output, pos, extraBytes.Length);
            Array.Copy(torrent, pos, output, pos+extraBytes.Length, torrent.Length-pos);

            InfoDone(string.Format("Added {0} trackers, now {1} total. (Avoided {2} duplicates)", trackers.Count, infile+trackers.Count, duplicates));
            
            /*
             * Write to temp. file
             */
            InfoStarting("Writing new torrent to temporary file");

            string path = Path.GetTempPath() + Guid.NewGuid() + ".torrent";
            File.WriteAllBytes(path, output);

            InfoDone();

            /*
             * Launch torrent client
             */
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine();
            Console.WriteLine("Press any key to open torrent in default application . . .");
            Console.ReadKey();

            System.Diagnostics.Process.Start(path);

        }
        
        static byte[] GetTorrentFromSite (string url)
        {
            //TODO: support for more sites
            if (!url.StartsWith("http://thepiratebay.org/torrent/"))
                return null;

            string[] parts = url.Split(new[] {'/'}, 5, StringSplitOptions.RemoveEmptyEntries);
            string torrentUrl = string.Format("http://torrents.thepiratebay.org/{0}/{1}.{0}.TPB.torrent", parts[3], parts[4]);

            return DownloadPage(torrentUrl);
        }

        static string DownloadPageText (string url)
        {
            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();

            StreamReader sr = new StreamReader(response.GetResponseStream());
            
            return sr.ReadToEnd();
        }

        static byte[] DownloadPage (string url)
        {
            WebRequest request = WebRequest.Create(url);
            WebResponse response = request.GetResponse();

            BinaryReader br = new BinaryReader(response.GetResponseStream());

            //TODO: better read
            byte[] buffer = new byte[response.ContentLength];
            int b;
            int pos = 0;
            while ((b = response.GetResponseStream().ReadByte()) != -1)
            {
                buffer[pos++] = (byte) b;
            }

            //br.Read(buffer, 0, (int)response.ContentLength);
            
            return buffer;

        }

        static void InfoStarting (string action)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(action.PadRight(Console.WindowWidth - 10));
        }

        private static void InfoDone ()
        {
            InfoDone(null);
        }

        static void InfoDone (string info)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[  O K  ]");
            
            if (info != null)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine("\t" + info);
            }
        }

    }
}
