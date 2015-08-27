using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CNU.UpdateMGMTClass
{
    public class FailingEventArgs
    {
        public string FailureReason;

        public FailingEventArgs(string eventData)
        {
            this.FailureReason = eventData;
        }
    }
}
