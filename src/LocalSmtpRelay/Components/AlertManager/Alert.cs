using System;
using System.Collections.Generic;
using System.Globalization;

namespace LocalSmtpRelay.Components.AlertManager
{
    public sealed class Alert
    {
        private const string AlertnameKey = "alertname";
        private const string SummaryKey = "summary";
        private const string DescriptionKey = "description";

        public Dictionary<string, string> Labels { get; } = new();

        public Dictionary<string, string> Annotations { get; } = new();

        public Uri? GeneratorURL { get; set; }

        /// <summary>
        /// A past value will resolve the alert.
        /// </summary>
        public string? EndsAt { get; set; }

        public void SetName(string name) => Labels[AlertnameKey] = name;

        public bool HasName() => Labels.ContainsKey(AlertnameKey);

        public bool HasSummary() => Annotations.ContainsKey(SummaryKey);

        public bool HasDescription() => Annotations.ContainsKey(DescriptionKey);

        public void SetEndsAt(DateTime utcDt)
        {
            if (utcDt.Kind != DateTimeKind.Utc)
                throw new ArgumentOutOfRangeException(nameof(utcDt));
            EndsAt = utcDt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
    }
}
