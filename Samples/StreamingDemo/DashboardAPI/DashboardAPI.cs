using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitterObservable;

namespace DashboardAPI
{
    public interface IDashboard
    {
        void OnNext(AnalyticsResultString next);
    }
}
