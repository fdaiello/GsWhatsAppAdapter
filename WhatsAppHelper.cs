// Helper para o WhatsApp Adapter
// Faz o meio de campo entre as requisições, a API do WhatsApp,
// e o serviços de reconhecimento e sintese de Voz

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GsWhatsAppAdapter
{
    /// <summary>
    /// A helper class to create Activities and WhatsApp messages.
    /// </summary>
    internal class WhatsAppHelper
    {
        private bool isspeechturn;
        private bool querystringused;
        private string textresponse;
        private readonly Uri _botUri;

        private readonly GsWhatsAppClient _gsWhatsAppClient;
        private readonly SpeechClient _speechClient;


        internal WhatsAppHelper(GsWhatsAppClient gsWhatsAppClient, SpeechClient speechClient, Uri botUri)
        {
            isspeechturn = false;
            querystringused = false;
            textresponse = string.Empty;

            _speechClient = speechClient;
            _gsWhatsAppClient = gsWhatsAppClient;
            _botUri = botUri;

        }
        /// <summary>
        /// Writes the HttpResponse.
        /// </summary>
        /// <param name="response">The httpResponse.</param>
        /// <param name="code">The status code to be written.</param>
        /// <param name="text">The text to be written.</param>
        /// <param name="encoding">The encoding for the text.</param>
        /// <param name="cancellationToken">A cancellation token for the task.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal async Task WriteAsync(HttpResponse response, int code, string text, Encoding encoding, CancellationToken cancellationToken)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (encoding == null)
            {
                throw new ArgumentNullException(nameof(encoding));
            }

            response.ContentType = "text/plain; charset=utf-8";
            response.StatusCode = code;

            text += textresponse;

            var data = encoding.GetBytes(text);

            await response.Body.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bot Framework <see cref="Activity"/> from an HTTP request that contains a GsWhatsApp message.
        /// </summary>
        /// <param name="payload">The HTTP request.</param>
        /// <returns>The activity object.</returns>
        internal async Task<Activity> PayloadToActivity(HttpRequest httpRequest, ILogger logger)
        {
            if (httpRequest == null)
            {
                throw new ArgumentNullException(nameof(httpRequest));
            }

            // Bot Activity que será construido com base na requisição http
            Activity activity;

            // Se tem parametros passados por Form
            if (httpRequest.HasFormContentType)
            {

                // Busca os parametros e salva em variaveis
                string botname = httpRequest.Form["botname"];
                string messageobj = httpRequest.Form["messageobj"];

                // Registra o POST no arquivo de LOG
                string input = "";
                if (botname != null)
                    input += "botname=" + botname;
                if (messageobj != null)
                    input += "messageobj=" + messageobj;

                // Grava em um arquivo de Log os dados recebidos
                logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), System.Environment.NewLine + "in: " + input);

                // Se recebeu messageobject
                if (messageobj != null)
                {

                    // Desserializa o objeto mensagem
                    GsMessageObj gsinmessage = JsonConvert.DeserializeObject<GsMessageObj>(messageobj);

                    // Confere se o tipo da mensagem é texto
                    if (gsinmessage.Type == "text" || gsinmessage.Type == "image")
                    {

                        // constroi a Activity com base no que veio na requisição
                        activity = ActivityBuilder(gsinmessage.Id, gsinmessage.From, gsinmessage.Type, gsinmessage.Text, botname, gsinmessage.Url);

                    }
                    else if (gsinmessage.Type == "voice" | gsinmessage.Type == "audio")
                    {
                        // Busca a stream com o audio via Gs API
                        Stream stream = await _gsWhatsAppClient.GetVoice(botname, gsinmessage.Voice.Id).ConfigureAwait(false);

                        // Tenta reconhecer o Audio
                        string speechtotext = await _speechClient.RecognizeOggStream(stream, gsinmessage.Voice.Id).ConfigureAwait(false);

                        // Se Reconheceu com sucesso
                        if (!string.IsNullOrEmpty(speechtotext) && speechtotext != "NOMATCH" && !speechtotext.StartsWith("CANCELED",StringComparison.OrdinalIgnoreCase))
                        {
                            // Sinaliza que o turno tem conversa
                            isspeechturn = true;

                            // constroi a Activity com base no que veio na requisição
                            activity = ActivityBuilder(gsinmessage.Id, gsinmessage.From, "text", speechtotext, botname);

                        }
                        else
                            // constroi a Activity com base no que veio na requisição
                            activity = ActivityBuilder(gsinmessage.Id, gsinmessage.From, gsinmessage.Type, gsinmessage.Text, botname, null, gsinmessage.Voice.Id, stream);

                    }
                    else if (gsinmessage.Type == "event")
                        // constroi a Activity com base no que veio na requisição
                        activity = ActivityBuilder(gsinmessage.Id, gsinmessage.Id, gsinmessage.From, gsinmessage.Type, gsinmessage.Text, botname, null);

                    else
                    { 
                        // constroi a Activity com base no que veio na requisição
                        activity = ActivityBuilder(gsinmessage.Id, gsinmessage.From, gsinmessage.Type, gsinmessage.Text, botname, null);

                        // Grava em um arquivo de Log indicando que veio um tipo não esperado
                        logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), $"Tipo de mensagem não esperado: {gsinmessage.Type}");
                    }

                }
                else
                {
                    // Grava em um arquivo de Log com a mensagem respondida
                    logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), "Msg Obj Missing");

                    throw new ArgumentException("Msg Obj Missing");
                }
            }
            else
            {
                // Confere se vieram parametros corretos por querystring
                if (!string.IsNullOrEmpty(httpRequest.Query["text"]) && !string.IsNullOrEmpty(httpRequest.Query["from"]) && !string.IsNullOrEmpty(httpRequest.Query["id"]))
                {
                    // marca em flag pra saber lidar com o retorno
                    querystringused = true;

                    // monta atividade com base nos parametros query string
                    activity = ActivityBuilder(httpRequest.Query["id"],httpRequest.Query["from"], "text", httpRequest.Query["text"], DateTime.Now.ToString(), "");
                }
                else
                {
                    // Grava em um arquivo de Log com a mensagem respondida
                    logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), "Callback deve ser chamado via POST");

                    throw new ArgumentException("Callback deve ser chamado via POST");
                }
            }

            // Retorna a activity
            return (activity);

        }

        // Envia uma mensagem para o Bot
        private static Activity ActivityBuilder(string messageId, string from, string type, string text, string botname, [Optional] string url, [Optional] string voiceid, [Optional] Stream stream)
        {
            // Instancia uma nova Activity
            Activity activity = new Activity
            {
                Id = messageId,
                Timestamp = DateTime.UtcNow,
                ChannelId = "whatsapp",
                Conversation = new ConversationAccount()
                {
                    Id = from,
                },
                From = new ChannelAccount()
                {
                    Id = from,
                },
                Recipient = new ChannelAccount()
                {
                    Id = botname,
                },
                Type = ActivityTypes.Message,
            };

            // Verifica o tipo da mensagem e os atributos que devem ser atribuidos
            if (type == "text")
            {
                activity.Text = text;
                activity.Attachments = null;
            }
            else if (type == "image")
            {
                activity.Attachments = new Attachment[1];
                activity.Attachments[0] = CreateAttachment(url, "image/png");

            }
            else if (type == "voice")
            {
                activity.Attachments = new Attachment[1];
                activity.Attachments[0] = CreateInlineAttachment(voiceid, "audio/ogg", stream);
            }
            else if (type == "event")
            {
                activity.Type = ActivityTypes.Event;
                activity.Attachments = null;
            }

            return (activity);

        }
		// Envia uma atividade para o WhatsApp
		public async Task<string> SendActivityToWhatsApp(Activity activity)
		{
            string id = string.Empty;

			// Se for uma mensagem de texto
			if (activity.Text != null)
			{
				// Recupera a mensagem
				string reply = activity.Text;

				// Se está conversando por Audio
				if (isspeechturn)
					// envia o texto como Audio
					id = await SendVoice(activity.Text, activity.Id, activity.Recipient.Id).ConfigureAwait(false);

				// Confere se tem Suggested Actions ( ações sugeridas )
				if (activity.SuggestedActions != null && activity.SuggestedActions.Actions.Count() > 0)
				{
					// Adiciona a resposta as ações sugeridas
					foreach (CardAction sugestedaction in activity.SuggestedActions.Actions)
					{
						reply += "\n     ```" + sugestedaction.Title + "```";
					}
				}

				// Envia a mensagem recebida do Bot de volta para o usuario
				if (querystringused)
				{
					// Se veio requisição por Query String ( geralmente teste ), devolve na requisição http
					if (!string.IsNullOrEmpty(textresponse)) textresponse += "\n";
                    textresponse += reply;
				}
				else
					// Se não é teste, envia via API ( se enviar na mesma requisição, fica grudado na mesma mensagem )
					id = await _gsWhatsAppClient.SendText(activity.Recipient.Id, reply).ConfigureAwait(false);

			}

			// Are there any attachments?
			if (activity.Attachments != null)
			{
				// Extract each attachment from the activity.
				foreach (Attachment attachment in activity.Attachments)
				{
					// Processa anexos recebidos do BOT e que devem ser enviados para o cliente
					switch (attachment.ContentType)
					{
						case "image/png":
						case "image/jpg":
						case "image/jpeg":
							// Chama o método para enviar a imagem para o cliente via Gushup API
							id = await _gsWhatsAppClient.SendMedia(activity.Recipient.Id, GsWhatsAppClient.Mediatype.image, attachment.Name, new Uri(attachment.ContentUrl), new Uri(attachment.ThumbnailUrl)).ConfigureAwait(false);
                            break;

						case "application/pdf":
							// Chama o método para enviar o video para o cliente via Gushup API
							id = await _gsWhatsAppClient.SendMedia(activity.Recipient.Id, GsWhatsAppClient.Mediatype.file, attachment.Name, new Uri(attachment.ContentUrl)).ConfigureAwait(false);
                            break;

						case "video/mpeg":
							// Chama o método para enviar o video para o cliente via Gushup API
							id = await _gsWhatsAppClient.SendMedia(activity.Recipient.Id, GsWhatsAppClient.Mediatype.video, attachment.Name, new Uri(attachment.ContentUrl)).ConfigureAwait(false);
                            break;

						case "audio/ogg":
							// Chama o método para enviar o video para o cliente via Gushup API
							id = await _gsWhatsAppClient.SendMedia(activity.Recipient.Id, GsWhatsAppClient.Mediatype.audio, attachment.Name, new Uri(attachment.ContentUrl)).ConfigureAwait(false);
                            break;

						case "audio/mp3":
							// Chama o método para enviar o video para o cliente via Gushup API
							id = await _gsWhatsAppClient.SendMedia(activity.Recipient.Id, GsWhatsAppClient.Mediatype.audio, attachment.Name, new Uri(attachment.ContentUrl)).ConfigureAwait(false);
                            break;

						case "application/vnd.microsoft.card.hero":
							// Se é conversa por audio
							if (isspeechturn)
								// Envia o texto do HeroCard por audio
								id = await SendVoiceFromHeroText(attachment, activity.Id, activity.Recipient.Id).ConfigureAwait(false);

							// Se é teste 
							if (querystringused)
							{
								// devolve via http
								if (!string.IsNullOrEmpty(textresponse)) textresponse += "\n";
								textresponse += ConvertHeroCardToWhatsApp(attachment);
							}
							else
								// envia para o cliente via API
								id = await _gsWhatsAppClient.SendText(activity.Recipient.Id, ConvertHeroCardToWhatsApp(attachment)).ConfigureAwait(false);

							break;

					}
				}
			}

            return id;
		}

        // Converte um HeroCard em texto puro
        private static string ConvertHeroCardToWhatsApp(Attachment attachment)
        {

            var heroCard = JsonConvert.DeserializeObject<HeroCard>(JsonConvert.SerializeObject(attachment.Content));

            string waoutput = "";

            if (heroCard != null)
            {
                if (!string.IsNullOrEmpty(heroCard.Title))
                    waoutput += "*" + heroCard.Title + "*\n";

                if (!string.IsNullOrEmpty(heroCard.Text))
                    waoutput += heroCard.Text + "\n";

                if (!string.IsNullOrEmpty(waoutput))
                    waoutput += "\n";

                if (heroCard.Buttons != null)
                    foreach (CardAction button in heroCard.Buttons)
                        waoutput += BoldFirstDigit(button.Title) + "\n";

                if (heroCard.Images != null)
                    foreach (CardImage image in heroCard.Images)
                        waoutput += image.Url + "\n";
            }
            return waoutput;
        }

        // Pega o título de um Hero Card e envia como Voz
        private async Task<string> SendVoiceFromHeroText(Attachment herocard, string textid, string usernumber)
        {

            var heroCard = JsonConvert.DeserializeObject<HeroCard>(herocard.Content.ToString());
            if (heroCard != null)
            {
                if (!string.IsNullOrEmpty(heroCard.Text))
                    return await SendVoice(heroCard.Text, textid, usernumber);
            }

            return string.Empty;
        }
        // Se a linha começa com um digito ( padrão de menu )...
        //    Adiciona um asterisco antes e outro depois do numero - negrito no whats app
        private static string BoldFirstDigit(string line)
        {
            return line.Substring(0, 1).Replace("1", "*1*").Replace("2", "*2*").Replace("3", "*3*").Replace("4", "*4*").Replace("5", "*5*").Replace("6", "*6*").Replace("7", "*7*").Replace("8", "*8*").Replace("9", "*9*") + line.Substring(1);
        }

        // Creates an <see cref="Attachment"/> to be sent from the bot to the user from a HTTP URL.
        private static Attachment CreateAttachment(string url, string contenttype)
        {
            // ContentUrl must be HTTPS.
            return new Attachment
            {
                Name = url.Split('/').Last(),
                ContentType = contenttype,
                ContentUrl = url
            };
        }

        // Cria um Inline Attachment - o conteudo vai dentro do Attachmento, ao inves de uma URL
        private static Attachment CreateInlineAttachment(string voiceid, string contenttype, Stream stream)
        {
            // Copia a stream para um array de bytes - tem que passar por um MemoryStream
            byte[] bytes = ConverteStreamToByteArray(stream);

            // Gera string com o array de bytes codificado em base64
            string base64 = Convert.ToBase64String(bytes);

            // ContentUrl Leva os dados inline no formato: "contentUrl": "data:audio/ogg;base64,iVBORw0KGgo…",
            return new Attachment
            {
                Name = voiceid + "." + contenttype.Split("/").Last(),
                ContentType = contenttype,
                ContentUrl = "data:" + contenttype + ";base64," + base64
            };
        }
        private static byte[] ConverteStreamToByteArray(Stream stream)
        {
            using MemoryStream mStream = new MemoryStream();
            stream.CopyTo(mStream);
            return mStream.ToArray();
        }
        // Converte um texto em Audio, converte para MP3, e envia
        private async Task<string> SendVoice(string text, string textid, string usernumber)
        {
            // Tira o pipe do Id
            textid = textid.Replace("|", "");

            // Retira caracteres Unicode ( pra não falar os Emojis )
            const int MaxAnsiCode = 255;
            if (text.Any(c => c > MaxAnsiCode))
            {
                string cleantext = string.Empty;
                for (int x = 0; x < text.Length; x++)
                    if ((int)text[x] <= MaxAnsiCode)
                        cleantext += text[x];
                text = cleantext;
            }

            // Tenta converter o texto para audio
            if (await _speechClient.TextToSpeechAsync(text, textid))
            {
                // Se conseguiu sintetizar o audio com base no texto, converte Wav pra Ogg
                string filenamewav = Path.Combine(Environment.CurrentDirectory, $@"wwwroot\media\Audio_{textid}.wav");
                Mp3Converter.WaveToMP3(filenamewav, filenamewav.Replace(".wav", ".mp3"));

                // Chama o método para enviar o video para o cliente via Gushup API
                string mediaurl = _botUri.ToString() + $@"/media/Audio_{textid}.mp3";
                return await _gsWhatsAppClient.SendMedia(usernumber, GsWhatsAppClient.Mediatype.audio, filenamewav.Replace(".wav", ""), new Uri(mediaurl));
            }
            else
                return string.Empty;
        }
        // Mensagem GupShup vinda no CallBAck da API
        private class GsMessageObj
        {
            public string From { get; set; }
            public string Id { get; set; }
            public string Text { get; set; }
            public string Timestamp { get; set; }
            public string Type { get; set; }
            public string Url { get; set; }
            public string Imgdata { get; set; }
            public GsVoice Voice { get; set; }
        }
        // Mensagem de Voz - padrão GupShup
        private class GsVoice
        {
            public string Id { get; set; }
            public string Mime_type { get; set; }
            public string Sha256 { get; set; }
        }
    }
}
