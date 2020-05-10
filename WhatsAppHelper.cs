// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace GsWhatsAppAdapter
{
    /// <summary>
    /// A helper class to create Activities and WhatsApp messages.
    /// </summary>
    internal static class WhatsAppHelper
    {
        /// <summary>
        /// Creates WhatsApp message options object from a Bot Framework <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity">The activity.</param>
        /// <param name="GsWhatsAppNumber">The GsWhatsApp phone number assigned to the bot.</param>
        /// <returns>The GsWhatsApp message options object.</returns>
        /// <seealso cref="GsWhatsAppAdapter.SendActivitiesAsync(ITurnContext, Activity[], System.Threading.CancellationToken)"/>
        public static CreateMessageOptions ActivityToGsWhatsApp(Activity activity, string GsWhatsAppNumber)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            if (string.IsNullOrWhiteSpace(GsWhatsAppNumber))
            {
                throw new ArgumentNullException(nameof(GsWhatsAppNumber));
            }

            var mediaUrls = new List<Uri>();
            if (activity.Attachments != null)
            {
                mediaUrls.AddRange(activity.Attachments.Select(attachment => new Uri(attachment.ContentUrl)));
            }

            var messageOptions = new CreateMessageOptions(activity.Conversation.Id)
            {
                ApplicationSid = activity.Conversation.Id,
                From = GsWhatsAppNumber,
                Body = activity.Text,
                MediaUrl = mediaUrls,
            };

            return messageOptions;
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
        public static async Task WriteAsync(HttpResponse response, int code, string text, Encoding encoding, CancellationToken cancellationToken)
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

            response.ContentType = "text/plain";
            response.StatusCode = code;

            var data = encoding.GetBytes(text);

            await response.Body.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a Bot Framework <see cref="Activity"/> from an HTTP request that contains a GsWhatsApp message.
        /// </summary>
        /// <param name="payload">The HTTP request.</param>
        /// <returns>The activity object.</returns>
        public static Activity PayloadToActivity(Dictionary<string, string> payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }
            
            var GsWhatsAppMessage = JsonConvert.DeserializeObject<GsWhatsAppMessage>(JsonConvert.SerializeObject(payload));

            return new Activity()
            {
                Id = GsWhatsAppMessage.MessageSid,
                Timestamp = DateTime.UtcNow,
                ChannelId = Channels.GsWhatsApp,
                Conversation = new ConversationAccount()
                {
                    Id = GsWhatsAppMessage.From ?? GsWhatsAppMessage.Author,
                },
                From = new ChannelAccount()
                {
                    Id = GsWhatsAppMessage.From ?? GsWhatsAppMessage.Author,
                },
                Recipient = new ChannelAccount()
                {
                    Id = GsWhatsAppMessage.To,
                },
                Text = GsWhatsAppMessage.Body,
                ChannelData = GsWhatsAppMessage,
                Type = ActivityTypes.Message,
                Attachments = int.TryParse(GsWhatsAppMessage.NumMedia, out var numMediaResult) && numMediaResult > 0 ? GetMessageAttachments(numMediaResult, payload) : null,
            };
        }

        /// <summary>
        /// Gets attachments from a GsWhatsApp message.
        /// </summary>
        /// <param name="numMedia">The number of media items to pull from the message body.</param>
        /// <param name="message">A dictionary containing the GsWhatsApp message elements.</param>
        /// <returns>An Attachments array with the converted attachments.</returns>
        public static List<Attachment> GetMessageAttachments(int numMedia, Dictionary<string, string> message)
        {
            var attachments = new List<Attachment>();
            for (var i = 0; i < numMedia; i++)
            {
                // Ensure MediaContentType and MediaUrl are present before adding the attachment
                if (message.ContainsKey($"MediaContentType{i}") && message.ContainsKey($"MediaUrl{i}"))
                {
                    var attachment = new Attachment()
                    {
                        ContentType = message[$"MediaContentType{i}"],
                        ContentUrl = message[$"MediaUrl{i}"],
                    };
                    attachments.Add(attachment);
                }
            }

            return attachments;
        }

        /// <summary>
        /// Converts a query string to a dictionary with key-value pairs.
        /// </summary>
        /// <param name="query">The query string to convert.</param>
        /// <returns>A dictionary with the query values.</returns>
        public static Dictionary<string, string> QueryStringToDictionary(string query)
        {
            var values = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(query))
            {
                return values;
            }

            var pairs = query.Replace("+", "%20").Split('&');

            foreach (var p in pairs)
            {
                var pair = p.Split('=');
                var key = pair[0];
                var value = Uri.UnescapeDataString(pair[1]);

                values.Add(key, value);
            }

            return values;
        }
    }
}
