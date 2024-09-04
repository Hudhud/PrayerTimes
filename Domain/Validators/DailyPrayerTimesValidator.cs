using Domain.Models;
using FluentValidation;

namespace Domain.Validators
{
    public class DailyPrayerTimesValidator : AbstractValidator<DailyPrayerTimes>
    {
        public DailyPrayerTimesValidator()
        {
            RuleFor(x => x.Date)
                .NotEmpty()
                .WithMessage("Date is required.");

            RuleFor(x => x.FajrTime)
                .NotEmpty()
                .WithMessage("Fajr time is required.");

            RuleFor(x => x.SunriseTime)
                .NotEmpty()
                .WithMessage("Sunrise time is required.");

            RuleFor(x => x.DhuhrTime)
                .NotEmpty()
                .WithMessage("Dhuhr time is required.");

            RuleFor(x => x.AsrTime)
                .NotEmpty()
                .WithMessage("Asr time is required.");

            RuleFor(x => x.MaghribTime)
                .NotEmpty()
                .WithMessage("Maghrib time is required.");

            RuleFor(x => x.IshaTime)
                .NotEmpty()
                .WithMessage("Isha time is required.");
        }
    }
}
