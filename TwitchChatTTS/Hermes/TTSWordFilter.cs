using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchChatTTS.Hermes
{
    public class TTSWordFilter
    {
        public string id { get; set; }
        public string search { get; set; }
        public string replace { get; set; }
        public string userId { get; set; }

        public bool IsRegex { get; set; }


        public TTSWordFilter() {
            IsRegex = true;
        }
    }
}