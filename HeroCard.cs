using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Bot.Schema;

namespace GsWhatsAppAdapter
{
	public class HeroCard
	{
		[JsonProperty(PropertyName = "title")]
		public string Title { get; set; }
		[JsonProperty(PropertyName = "subtitle")]
		public string Subtitle { get; set; }
		[JsonProperty(PropertyName = "text")]
		public string Text { get; set; }
		[JsonProperty(PropertyName = "images")]
		public IList<CardImage> Images { get; set; }
		[JsonProperty(PropertyName = "buttons")]
		public IList<CardAction> Buttons { get; set; }
		[JsonProperty(PropertyName = "tap")]
		public CardAction Tap { get; set; }
	}
}
