using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GsWhatsAppAdapter
{
	// GsApi
	// Classe para fazer as chamadas da API GupShup para envio de mensagems para o WhatsApp
	public class GsWhatsAppClient
	{

		// Variaveis de configuração para usar a API
		private string gsApiKey;
		private string whatsAppNumber;
		private Uri gsApiUri;
		private Uri gsMediaUri;

		// Constructor
		public GsWhatsAppClient(WhatsAppAdapterOptions options)
		{
			gsApiKey = options.GsApiKey;
			whatsAppNumber = options.WhatsAppNumber;
			gsApiUri = options.GsApiUri;
			gsMediaUri = options.GsMediaUri;
		}

		// Tipos de midia suportados
		public enum Mediatype
		{
			image,
			audio,
			file,
			video,
		}
		public async Task<Boolean> SendMedia(string destination, Mediatype mediatype, string filename, Uri contentUri, [Optional] Uri thumbnailUri)
		{

			if (contentUri == null)
			{
				throw new ArgumentException("ContentUri cannot be null");
			}

			HttpClient httpClient = new HttpClient();
			try
			{
				// Cabeçalhos Http
				httpClient.DefaultRequestHeaders.Add("User-Agent", "GsApi/1.0");
				httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
				httpClient.DefaultRequestHeaders.Add("Apikey", gsApiKey);
				httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

				// Monta o corpo da requisição
				string content = "channel=whatsapp&source=" + whatsAppNumber + "&destination=" + destination + "&message.payload={\"type\":\"" + mediatype.ToString() + "\",";
				if (mediatype != Mediatype.audio)
					content += "\"filename\":\"" + filename + "\",";
				if (mediatype == Mediatype.image)
				{
					content += "\"originalUrl\":\"" + contentUri + "\"";
					if (thumbnailUri != null)
						content += ",\"previewUrl\":\"" + thumbnailUri + "\"";
					else
						content += ",\"previewUrl\":\"" + contentUri + "\"";

					content += "}";
				}
				else
					content += "\"url\":\"" + contentUri.ToString() + "\"}";

				HttpContent httpContent = new StringContent(content, Encoding.UTF8);
				httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

				// Faz a requisição e lê a resposta de forma assíncrona
				var httpResponseMessage = await httpClient.PostAsync(gsApiUri, httpContent).ConfigureAwait(false);
				string resp = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

				httpContent.Dispose();
			}
			catch (Exception ex)
			{
				Debug.WriteLine("GsApi: SendImages: " + ex);
				httpClient.Dispose();
				return false;
			}

			httpClient.Dispose();
			return true;
		}

		public async Task<Boolean> SendText(string destination, string text)
		{

			HttpClient httpClient = new HttpClient();
			try
			{
				httpClient.DefaultRequestHeaders.Add("User-Agent", "GsApi/1.0");
				httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
				httpClient.DefaultRequestHeaders.Add("Apikey", gsApiKey);
				httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

				string content = "channel=whatsapp&source=" + whatsAppNumber + "&destination=" + destination + "&message=" + text;

				HttpContent httpContent = new StringContent(content, Encoding.UTF8);

				httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

				var httpResponseMessage = await httpClient.PostAsync(gsApiUri, httpContent).ConfigureAwait(false);
				string resp = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);

				httpContent.Dispose();

			}
			catch (Exception ex)
			{
				Debug.WriteLine("GsApi: SendText: " + ex);
				httpClient.Dispose();
				return false;
			}

			httpClient.Dispose();
			return true;
		}

		public async Task<Stream> GetVoice(string whatsAppAppname, string voiceID)
		{
			try
			{

				string uri = gsMediaUri + whatsAppAppname + "/" + voiceID;

				using var httpClient = new HttpClient();
				httpClient.DefaultRequestHeaders.Add("User-Agent", "GsApi/1.0");
				httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
				httpClient.DefaultRequestHeaders.Add("Apikey", gsApiKey);
				httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

				using var request = new HttpRequestMessage(HttpMethod.Get, uri);
				Stream contentStream = await (await httpClient.SendAsync(request).ConfigureAwait(false)).Content.ReadAsStreamAsync().ConfigureAwait(false);

				request.Dispose();
				httpClient.Dispose();
				return (contentStream);

			}
			catch (Exception ex)
			{
				Debug.WriteLine("GsApi: SendText: " + ex);
				Stream stream = new MemoryStream();
				return (stream);
			}
		}
		public async Task<Stream> GetAudio(Uri uri)
		{
			try
			{

				using var httpClient = new HttpClient();
				httpClient.DefaultRequestHeaders.Add("User-Agent", "GsApi/1.0");
				httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
				httpClient.DefaultRequestHeaders.Add("Apikey", gsApiKey);
				httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

				using var request = new HttpRequestMessage(HttpMethod.Get, uri);
				Stream contentStream = await (await httpClient.SendAsync(request).ConfigureAwait(false)).Content.ReadAsStreamAsync().ConfigureAwait(false);

				request.Dispose();
				httpClient.Dispose();
				return (contentStream);

			}
			catch (Exception ex)
			{
				Debug.WriteLine("GsApi: SendText: " + ex);
				Stream stream = new MemoryStream();
				return (stream);
			}
		}
	}
}
