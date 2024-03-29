﻿using Newtonsoft.Json;
using Microsoft.Bot.Schema;

namespace GsWhatsAppAdapter
{
	public class CardImage
	{
		[JsonProperty(PropertyName = "url")]
		public string Url { get; set; }
		[JsonProperty(PropertyName = "alt")]
		public string Alt { get; set; }
		[JsonProperty(PropertyName = "tap")]
		public CardAction Tap { get; set; }
	}
}
