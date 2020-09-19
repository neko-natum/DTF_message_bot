using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DTF_message_bot
{
    internal class HealthchecksOptions : IValidatableObject
    {
        public bool? HealthchecksEnabled { get; set; } = false;
        public string HealthcheckUri { get; set; }
        public string HealthcheckMessage { get; set; }
        public int? BotSelfId { get; set; }
        public double? HealthcheckIntervalInMinutes { get; set; }
        public string SenderAccountToken { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (HealthchecksEnabled ?? false)
            {
                if (string.IsNullOrWhiteSpace(HealthcheckUri))
                {
                    yield return new ValidationResult("Healthcheck URI must be provided", new[] { nameof(HealthcheckUri) });
                }

                if (string.IsNullOrWhiteSpace(HealthcheckMessage))
                {
                    yield return new ValidationResult("Healthcheck message must be provided", new[] { nameof(HealthcheckMessage) });
                }

                if (!BotSelfId.HasValue)
                {
                    yield return new ValidationResult("Bot self ID must be provided for healthchecks", new[] { nameof(BotSelfId) });
                }

                if (!HealthcheckIntervalInMinutes.HasValue)
                {
                    yield return new ValidationResult("Healthcheck interval must be provided", new[] { nameof(HealthcheckIntervalInMinutes) });
                }

                if (HealthcheckIntervalInMinutes <= 0)
                {
                    yield return new ValidationResult("Healthcheck interval must be a positive number", new[] { nameof(HealthcheckIntervalInMinutes) });
                }

                if (string.IsNullOrWhiteSpace(SenderAccountToken))
                {
                    yield return new ValidationResult("Healthcheck sender account token must be provided", new[] { nameof(SenderAccountToken) });
                }
            }

            yield return ValidationResult.Success;
        }
    }
}

