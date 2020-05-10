using System;
using System.Threading;
using System.Threading.Tasks;

namespace GsWhatsAppAdapter
{
    /// <summary>
    /// Wrapper class for the GsWhatsApp API.
    /// </summary>
    public class WhatsAppClientWrapper
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="WhatsAppClientWrapper"/> class.
        /// </summary>
        /// <param name="options">An object containing:
        /// WhatsAppNumber: The phone number associated with the WhatsApp Business account.
        /// GsApiKey: The API KEY from the account at GupShup.IO associated with WhatsApp number
        /// GsApiUri: URI for API calls
        /// GsMediaUri: URI for retreaving media
        public WhatsAppClientWrapper(WhatsAppAdapterOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrWhiteSpace(options.WhatsAppNumber))
            {
                throw new ArgumentException(nameof(options.WhatsAppNumber));
            }

            if (string.IsNullOrWhiteSpace(options.GsApiKey))
            {
                throw new ArgumentException(nameof(options.GsApiKey));
            }

            if (options.GsApiUri == null)
            {
                throw new ArgumentException(nameof(options.GsApiUri));
            }
            if (options.GsMediaUri == null)
            {
                throw new ArgumentException(nameof(options.GsMediaUri));
            }

            GsWhatsAppClient.Init(Options);
        }

        public WhatsAppAdapterOptions Options { get; }

        /// <summary>
        /// Sends a WhatsApp message.
        /// </summary>
        /// <param name="messageOptions">An object containing the parameters for the message to send.</param>
        /// <param name="cancellationToken">A cancellation token for the task.</param>
        /// <returns>The ID of the WhatsApp message sent.</returns>
        public virtual async Task<string> SendMessage(CreateMessageOptions messageOptions, CancellationToken cancellationToken)
        {
            var messageResource = await MessageResource.CreateAsync(messageOptions).ConfigureAwait(false);
            return messageResource.Sid;
        }

    }
}
