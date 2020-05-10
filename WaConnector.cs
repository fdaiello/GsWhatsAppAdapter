using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mp3Codec;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Universal.Common.Net.Http;
using Universal.Microsoft.Bot.Connector.DirectLine;

namespace GsWhatsAppAdapter
{
	// Processa um CallBack contendo uma mensagem de WhatsApp recebida pela API Oficial via GupShup
	// originada pelo cliente, com destino ao nosso BOT
	// Conecta com o BOT via DirectLineSecret
	// Envia resposta para o cliente via API Gupshup para envio de mensagens para o WhatsApp
	public class GsWhatsAppAdapter
	{
		// Instancia objeto para fazer chamadas a API GupShup
		private readonly GsWhatsAppClient _gsApi;

		// HasTable (Singleton) - para salvar em memória o numero do cliente, e manter sessao ativa - nao iniciar nova SessionStart a cada requisição
		private readonly Hashtable _htblFromList;

		// DirectLineClient - para se conectar ao Bot
		private readonly DirectLineClient _directLineClient;

		// Parametros para se conectar no Bot via DirectLine
		private readonly string _BotId;

		// Classe para reconhecer e sintetizar Voz
		private readonly Speech _speech;

		// Flag que indica se enviou Speech to Text
		private bool isspeechturn;

		// URL para encontrar os arquivos de audio
		private readonly string _MediaHomeUrl;

		// Indica que foi chamado por GET para testar
		private bool testing = false;

		// Logger
		private ILogger _logger;

		// Construtor da classe GsWhatsAppAdapter - le objetos e parametros passados por Injection
		public GsWhatsAppAdapter(GsWhatsAppClient gsApi, IOptions<BotSettings> botsettings, Hashtable HtblFromList, DirectLineClient directLineClient, Speech speech, ILogger logger)
		{
			_gsApi = gsApi;
			_htblFromList = HtblFromList;
			_BotId = botsettings == null ? null : botsettings.Value.BotId;
			_MediaHomeUrl = botsettings == null ? null : botsettings.Value.MediaHomeUrl;
			_directLineClient = directLineClient;
			_speech = speech;
			_logger = logger;
		}

		// EntryPoint - Processa uma requisição de CallBack
		public async Task<string> ProcessPost(HttpContext context)
		{
			try
			{
				string botname = null;
				string messageobj = null;
				isspeechturn = false;

				// Se tem parametros passados por Form
				if (context.Request.HasFormContentType)
				{
					// Busca os parametros e salva em variaveis
					botname = context.Request.Form["botname"];
					messageobj = context.Request.Form["messageobj"];
					// Registra o POST no arquivo de LOG
					string input = "";
					if (botname != null)
						input += "botname=" + botname;
					if (messageobj != null)
						input += "messageobj=" + messageobj;

					// Grava em um arquivo de Log os dados recebidos
					_logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), System.Environment.NewLine + "in: " + input);

					// Se recebeu messageobject
					if (messageobj != null)
					{
						// Desserializa o objeto mensagem
						GsMessageObj gsinmessage = JsonConvert.DeserializeObject<GsMessageObj>(messageobj);

						// Confere se o tipo da mensagem é texto
						if (gsinmessage.Type == "text" || gsinmessage.Type == "image")
						{
							// Create an Instance of the Chat object
							Chat objChat = new Chat();

							// Pass the message to the Bot and get the response
							objChat = await TalkToTheBot(gsinmessage.From, gsinmessage.Type, gsinmessage.Text, gsinmessage.Timestamp, gsinmessage.Url);

							// Retorna a função devolvendo uma resposta em branco - TalkToTheBot -> ReadBotMessages vai enviar resposta direto pro cliente
							return (objChat.ChatResponse);

						}
						else if (gsinmessage.Type == "voice")
						{
							// Create an Instance of the Chat object
							Chat objChat = new Chat();

							// Busca a stream com o audio via Gs API
							Stream stream = new MemoryStream();
							stream = await _gsApi.GetVoice(botname, gsinmessage.Voice.Id);

							// Tenta reconhecer o Audio
							string speechtotext = await _speech.RecognizeOggStream(stream, gsinmessage.Voice.Id);

							// Se Reconheceu com sucesso
							if (!string.IsNullOrEmpty(speechtotext) && speechtotext != "NOMATCH" && !speechtotext.StartsWith("CANCELED"))
							{
								// Salva em variavel da classe que o turno tem conversa
								isspeechturn = true;

								// Envia mensagem com o texto
								objChat = await TalkToTheBot(gsinmessage.From, "text", speechtotext, gsinmessage.Timestamp);
							}
							else
								// Envia mensagem INLINE - byte array dentro do post - e espera a resposta
								objChat = await TalkToTheBot(gsinmessage.From, gsinmessage.Type, gsinmessage.Text, gsinmessage.Timestamp, null, gsinmessage.Voice.Id, stream);

							// Retorna a função devolvendo uma resposta em branco - TalkToTheBot -> ReadBotMessages vai enviar resposta direto pro cliente
							return (null);

						}
						else if (gsinmessage.Type == "audio")
						{
							// Create an Instance of the Chat object
							Chat objChat = new Chat();

							// Busca a stream com o audio via Gs API
							Stream stream = new MemoryStream();
							stream = await _gsApi.GetAudio(gsinmessage.Url);

							// Tenta reconhecer o Audio
							string speechtotext = await _speech.RecognizeOggStream(stream, gsinmessage.Id);

							// Se Reconheceu com sucesso
							if (!string.IsNullOrEmpty(speechtotext) && speechtotext != "NOMATCH" && !speechtotext.StartsWith("CANCELED"))
							{
								// Salva em variavel da classe que o turno tem conversa
								isspeechturn = true;

								// Envia mensagem com o texto
								objChat = await TalkToTheBot(gsinmessage.From, "text", speechtotext, gsinmessage.Timestamp);
							}
							else
								// Envia mensagem INLINE - byte array dentro do post - e espera a resposta
								objChat = await TalkToTheBot(gsinmessage.From, gsinmessage.Type, gsinmessage.Text, gsinmessage.Timestamp, null, gsinmessage.Voice.Id, stream);

							// Retorna a função devolvendo uma resposta em branco - TalkToTheBot -> ReadBotMessages vai enviar resposta direto pro cliente
							return (null);

						}
						else if (gsinmessage.Type == "event")
							return ("true");

						else
						// Por enquanto, Não sabe lidar com outros tipos de anexo alem de voz ou imagem
						{
							// Mensagem de erro
							string msgAttachedInvalid = "Desculpe, por enquanto só consigo lidar com texto, imagem ou voz.";

							// Grava em um arquivo de Log com a mensagem respondida
							_logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), "out:" + msgAttachedInvalid);

							// No momento só aceita texto, imagem, ou voz
							return (msgAttachedInvalid);
						}

					}
					else
					{
						// Mensagem de erro
						string msgMessageObjectMissing = "Msg Obj Missing";

						// Grava em um arquivo de Log com a mensagem respondida
						_logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), "out: " + msgMessageObjectMissing);

						// Não localizaou messageobject que deveria ter sido passado como parametros POST ( Request.Form )
						return (msgMessageObjectMissing);
					}
				}
				else
				{
					// Confere se é teste
					if (!string.IsNullOrEmpty(context.Request.Query["text"]) && !string.IsNullOrEmpty(context.Request.Query["from"]))
					{
						// Obtem a mensagem enviada por parametro, e configura sessão de teste
						string texto = context.Request.Query["text"];
						testing = true;

						// Create an Instance of the Chat object
						Chat objChat = new Chat();

						// Pass the message to the Bot and get the response
						objChat = await TalkToTheBot(context.Request.Query["from"], "text", texto, DateTime.Now.ToString(), "");

						return (objChat.ChatResponse);
					}
					else

					{
						// Mensagem de Erro
						string msgMustPost = "Callback deve ser chamado via POST";

						// Grava em um arquivo de Log com a mensagem respondida
						_logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), "out: " + msgMustPost);

						// Foi chamada por GET
						return (msgMustPost);
					}
				}
			}

			catch (HttpException ex)
			{
				var MsgErro = ex.Message.ToString();
				_logger.LogError("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), MsgErro);

				MsgErro = "Desculpe, ocorreu uma falha na conexão.";
				return (MsgErro);
			}

			catch (System.Exception ex)
			{
				var MsgErro = ex.Message.ToString();
				var MsgErro2 = "";
				if (ex.InnerException != null)
					MsgErro2 = ex.InnerException.ToString();
				_logger.LogError("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), MsgErro + " " + MsgErro2);

				MsgErro = "Desculpe, ocorreu uma falha no sistema.";
				return (MsgErro);
			}
		}

		// Envia uma mensagem para o Bot
		private async Task<Chat> TalkToTheBot(string from, string type, string text, string gsmessageTimestamp, [Optional] string url, [Optional] string voiceid, [Optional] Stream stream)
		{
			// Watermark é usado pelo DirectLineClient para saber quais mensagens já foram lidas e quais devem ser enviadas. Apesar de ser string, contem um número sequencial
			string watermark = null;

			// Try to get the existing Conversation
			Conversation conversation;

			// Verfica se tem este numero na lista - Consulta o HasTable estatico declarado em StartUp
			if (_htblFromList[from] != null)
			{
				// Busca os valores salvos
				FromSession thisfromsession = _htblFromList[from] as FromSession;
				conversation = thisfromsession.Conversation;

				// Try to get an existing watermark 
				// the watermark marks the last message we received
				watermark = thisfromsession.Watermark;

			}
			// Se não encontrou registro deste usurio ( from )
			else
			{
				// There is no existing conversation
				// start a new one
				conversation = await _directLineClient.StartConversationAsync().ConfigureAwait(false);

				// Se deu erro na criação de uma nova conversa
				if (conversation == null)
					return (new Chat { ChatMessage = "", ChatResponse = "Ocorreu um ERRO ao estabelecer a conexão com o Bot", Watermark = "" });

			}

			// Instancia uma nova Activity - para passar a mensagem: texto e/ou midia
			Activity activity = new Activity
			{
				From = new ChannelAccount { Id = from },
				Type = ActivityTypes.Message,
				Id = gsmessageTimestamp,
				Timestamp = DateTime.Now.ToString("HH:mm:ss"),
				ChannelId = "whatsapp"
			};

			// Verifica o tipo da mensagem e os atributos que devem ser atribuidos
			if (type == "text")
			{
				activity.Text = text;
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
			}

			bool timeout = false;
			bool othererror = false;
			string msgerro = string.Empty;
			try
			{
				// Post the message to the Bot
				await _directLineClient.PostActivityAsync(conversation.ConversationId, activity).ConfigureAwait(false);

			}
			catch (HttpException ex)
			{
				// Registra em log que deu erro
				var MsgErro = ex.Message.ToString();
				_logger.LogError("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), MsgErro);

				// Se foi TimeOut
				if (ex.Message.Contains("timed out"))
				{
					// Avisa o usuário
					await _gsApi.SendText(from, "Oi! Por favor, aguarde um instante ... estou inicializando a minha conexão ...");

					// Esperamos mais um pouco
					Thread.Sleep(5000);
					timeout = true;
				}
				else
					othererror = true;

			}

			// Get the response as a Chat object
			Chat objChat = await ReadBotMessagesAsync(_directLineClient, conversation.ConversationId, watermark, from);

			// Se deu timeout e não teve resposta do Bot
			if (timeout & string.IsNullOrEmpty(objChat.ChatResponse))
			{
				// Avisa o usuário
				await _gsApi.SendText(from, "Só mais um pouquinho ...");

				// Esperamos mais um pouco
				Thread.Sleep(5000);

				// Consultamos novamente o Bot pra ver se chegou a resposta.
				objChat = await ReadBotMessagesAsync(_directLineClient, conversation.ConversationId, watermark, from);

				// Se ainda não veio resposta
				if (string.IsNullOrEmpty(objChat.ChatResponse))
					msgerro = "Desculpe, ocorreu uma falha na minha conexão. Tente novamente mais tarde.";

			}
			else if (othererror)
				msgerro = "Desculpe, estou em manutenção. Por favor, tente mais tarde.";


			if (!string.IsNullOrEmpty(objChat.Watermark))
			{
				// Cria um objeto de sessao
				FromSession fromsession = new FromSession
				{
					Lastdatetime = DateTime.Now,
					Conversation = conversation,
					Watermark = objChat.Watermark
				};

				// Verfica se tem este numero na HashList - previamente alimentada com variavel de aplicação
				if (_htblFromList[from] == null)
				{
					_htblFromList.Add(from, fromsession);
				}
				else
				{
					_htblFromList[from] = fromsession;
				}

			}
			else
				objChat.ChatResponse = msgerro;

			// Retorna o ObjChat - usado para logar a resposta
			return (objChat);

		}

		// Le a(s) resposta(s) recebidas do bot, e envia para o cliente no WhatsApp
		private async Task<Chat> ReadBotMessagesAsync(DirectLineClient client, string conversationId, string watermark, string usernumber)
		{

			// Create an Instance of the Chat object
			Chat objChat = new Chat();

			// Retrieve the activity set from the bot.
			var activitySet = await client.GetActivitiesAsync(conversationId, watermark);

			// Set the watermark to the message received
			watermark = activitySet?.Watermark;

			// Extract the activies sent from our bot.
			var activities = (from activity in activitySet.Activities
							  where activity.From.Id == _BotId
							  select activity
							  ).ToList();

			// Consulta em loop todas as activities recebidas como respostas do Bot
			int loopcount = 0;
			bool filesent = false;
			foreach (Activity activity in activities)
			{
				// Se for uma mensagem de texto
				if (activity.Text != null)
				{
					// Recupera a mensagem
					string reply = activity.Text;

					// Se está conversando por Audio
					if (isspeechturn )
						// envia o texto como Audio
						await SendVoice(reply, activity.Id, usernumber);

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
					if (testing)
					{
						// Se é teste, devolve na requisição http
						if (!string.IsNullOrEmpty(objChat.ChatResponse)) objChat.ChatResponse += "\n";
						objChat.ChatResponse += reply;
					}
					else
						// Se não é teste, envia via API ( se enviar na mesma requisição, fica grudado na mesma mensagem )
						await _gsApi.SendText(usernumber, reply);

					// Grava em um arquivo de Log a mensagem respondida
					_logger.LogInformation("GsWhatsAppAdapter-" + System.DateTime.Today.ToString("yyyyMMdd"), $"to: {usernumber}, reply: {reply}" );
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
								await _gsApi.SendMedia(usernumber, GsWhatsAppClient.Mediatype.image, attachment.Name, attachment.ContentUrl, attachment.ThumbnailUrl);
								filesent = true;
								break;

							case "application/pdf":
								// Chama o método para enviar o video para o cliente via Gushup API
								await _gsApi.SendMedia(usernumber, GsWhatsAppClient.Mediatype.file, attachment.Name, attachment.ContentUrl);
								filesent = true;
								break;

							case "video/mpeg":
								// Chama o método para enviar o video para o cliente via Gushup API
								await _gsApi.SendMedia(usernumber, GsWhatsAppClient.Mediatype.video, attachment.Name, attachment.ContentUrl);
								filesent = true;
								break;

							case "audio/ogg":
								// Chama o método para enviar o video para o cliente via Gushup API
								await _gsApi.SendMedia(usernumber, GsWhatsAppClient.Mediatype.audio, attachment.Name, attachment.ContentUrl);
								filesent = true;
								break;

							case "audio/mp3":
								// Chama o método para enviar o video para o cliente via Gushup API
								await _gsApi.SendMedia(usernumber, GsWhatsAppClient.Mediatype.audio, attachment.Name, attachment.ContentUrl);
								filesent = true;
								break;

							case "application/vnd.microsoft.card.hero":
								// Se é conversa por audio
								if (isspeechturn)
									// Envia o texto do HeroCard por audio
									await SendVoiceFromHeroText(attachment, activity.Id, usernumber);

								// Se é teste 
								if (testing)
								{
									// devolve via http
									if (!string.IsNullOrEmpty(objChat.ChatResponse)) objChat.ChatResponse += "\n";
									objChat.ChatResponse += ConvertHeroCardToWhatsApp(attachment);
								}
								else
									// envia para o cliente via API
									await _gsApi.SendText(usernumber, ConvertHeroCardToWhatsApp(attachment));

								break;

						}
					}
				}
				// Se tem mais atividades, espera pra nao grudar as mensagens ( se enviou arquivo espera um pouco mais )
				loopcount++;
				if (activities.Count > loopcount )
				{
					Task.Delay(filesent ? 2000 : 1000).Wait();
					filesent = false;
				}
			}

			// Atualiza o Watermark ( contador sequencial de mensagens usado pelo DirectLine )
			objChat.Watermark = watermark;

			// Devolve o objeto que contem o que foi enviado, a resposta, e o watermark
			return objChat;
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

		// Converte um HeroCard em texto puro
		private static string ConvertHeroCardToWhatsApp(Attachment attachment)
		{
			var heroCard = JsonConvert.DeserializeObject<HeroCard>(attachment.Content.ToString());
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
		private static string BoldFirstDigit(string line)
		{
			return line.Substring(0, 1).Replace("1", "*1*").Replace("2", "*2*").Replace("3", "*3*").Replace("4", "*4*").Replace("5", "*5*").Replace("6", "*6*").Replace("7", "*7*").Replace("8", "*8*").Replace("9", "*9*") + line.Substring(1);
		}

		// Converte um texto em Audio, converte para MP3, e envia
		private async Task SendVoice(string text, string textid, string usernumber)
		{
			// Tira o pipe do Id
			textid = textid.Replace("|", "");

			// Retira caracteres Unicode ( pra não falar os Emojis )
			const int MaxAnsiCode = 255;
			if ( text.Any( c => c > MaxAnsiCode) )
			{
				string cleantext = string.Empty;
				for (int x = 0; x < text.Length; x++)
					if ((int)text[x] <= MaxAnsiCode)
						cleantext += text[x];
				text = cleantext;
			}

			// Tenta converter o texto para audio
			if (await _speech.TextToSpeechAsync(text, textid))
			{
				// Se conseguiu sintetizar o audio com base no texto, converte Wav pra Ogg
				string filenamewav = Path.Combine(Environment.CurrentDirectory, $@"wwwroot\media\Audio_{textid}.wav");
				Mp3Converter.WaveToMP3(filenamewav, filenamewav.Replace(".wav", ".mp3"));

				// Chama o método para enviar o video para o cliente via Gushup API
				string mediaurl = _MediaHomeUrl + $@"media/Audio_{textid}.mp3";
				await _gsApi.SendMedia(usernumber, GsWhatsAppClient.Mediatype.audio, filenamewav.Replace(".wav", ""), mediaurl);
			}
		}

		// Converte um HeroCard em texto puro
		private async Task SendVoiceFromHeroText(Attachment herocard, string textid, string usernumber)
		{

			var heroCard = JsonConvert.DeserializeObject<HeroCard>(herocard.Content.ToString());
			if (heroCard != null)
			{
				if (!string.IsNullOrEmpty(heroCard.Text))
					await SendVoice(heroCard.Text, textid, usernumber);
			}

			return;
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
		// Chat - para o DirectLine
		private class Chat
		{
			public string ChatMessage { get; set; }
			public string ChatResponse { get; set; } = String.Empty;
			public string Watermark { get; set; }
		}
		// Salva valores de sessão
		private class FromSession
		{
			public string Watermark { get; set; }
			public Conversation Conversation { get; set; }
			public DateTime Lastdatetime { get; set; }
		}
	}
}
