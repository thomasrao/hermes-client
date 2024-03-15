// using System.Text.RegularExpressions;
// using HermesSocketLibrary.Request.Message;
// using TwitchChatTTS.Hermes;

// namespace TwitchChatTTS.Twitch
// {
//     public class TTSContext
//     {
//         public string DefaultVoice;
//         public IEnumerable<TTSVoice>? EnabledVoices;
//         public IDictionary<string, TTSUsernameFilter>? UsernameFilters;
//         public IEnumerable<TTSWordFilter>? WordFilters;
//         public IList<VoiceDetails>? AvailableVoices { get => _availableVoices; set { _availableVoices = value; EnabledVoicesRegex = GenerateEnabledVoicesRegex(); } }
//         public IDictionary<long, string>? SelectedVoices;
//         public Regex? EnabledVoicesRegex;

//         private IList<VoiceDetails>? _availableVoices;


//         private Regex? GenerateEnabledVoicesRegex() {
//             if (AvailableVoices == null || AvailableVoices.Count() <= 0) {
//                 return null;
//             }

//             var enabledVoicesString = string.Join("|", AvailableVoices.Select(v => v.Name));
//             return new Regex($@"\b({enabledVoicesString})\:(.*?)(?=\Z|\b(?:{enabledVoicesString})\:)", RegexOptions.IgnoreCase);
//         }
//     }
// }