using Domain.Models;
using FluentValidation;

namespace Domain.Validators
{
    public class CityPrayerTimesValidator : AbstractValidator<CityPrayerTimes>
    {
        public CityPrayerTimesValidator()
        {
            RuleFor(x => x.City)
                .NotEmpty()
                .WithMessage("City name is required.");

            RuleFor(x => x.PrayerTimes)
                .NotEmpty()
                .WithMessage("Prayer times collection cannot be empty.");
        }
    }
}
