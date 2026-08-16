using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Domain.Enums
{
    public enum SubscriptionStatus
    {
        Active=1,
        Expired=2,
        Susended=3
    }

    public enum SubscriptionPlan
    {
        Trial=7,
        FifteenDays=15,
        ThirtyDays=30,
        NinetyDays=90
    }
}
