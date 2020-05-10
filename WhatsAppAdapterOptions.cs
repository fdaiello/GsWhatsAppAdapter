using System;

namespace GsWhatsAppAdapter
{
    /// <summary>
    /// Defines values that a <see cref="GsWhatsAppClientWrapper"/> can use to connect to WhatsApp's using GupShup API.
    /// </summary>
    public class WhatsAppAdapterOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WhatsAppAdapterOptions"/> class.
        /// </summary>
        /// <param name="WhatsAppNumber">The WhatsApp phone number.</param>
        /// <param name="GsApiKey">The KEY for using the API from GupShup Account.</param>
        /// <param name="GsApiUri">URI for API calls.</param>
        /// <param name="GsMediaUri">URI for retreaving media.</param>
        public WhatsAppAdapterOptions(string whatsAppNumber, string gsApiKey, Uri gsApiUri, Uri gsMediaUri)
        {
            WhatsAppNumber = whatsAppNumber;
            GsApiKey = gsApiKey;
            GsApiUri = gsApiUri;
            GsMediaUri = gsMediaUri;
        }

        /// <summary>
        /// Gets or sets the phone number associated with this WhatsApp Business API.
        /// </summary>
        /// <value>
        /// The phone number, in the format 1XXXYYYZZZZ.
        /// </value>
        public string WhatsAppNumber { get; set; }

        /// <summary>
        /// Gets or sets API KEY from the GupShup account.
        /// </summary>
        /// <value>The account SID.</value>
        public string GsApiKey { get; set; }

        /// <summary>
        /// Gets or sets the URI for the API calls
        /// </summary>
        public Uri GsApiUri { get; set; }

        /// <summary>
        /// Gets or sets the URL for getting media files
        /// </summary>
        public Uri GsMediaUri { get; set; }
    }
}
