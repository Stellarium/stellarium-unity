// GZ Some formatting routines for Degrees/Minutes/Seconds strings.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DMSFormatter {
	
	public static string DMSstring(float degrees, bool withSeconds=true, bool withSecondsDecimal=true)
	{
        float sign = Mathf.Sign(degrees);
        degrees = Mathf.Abs(degrees);
		int fullDegrees=Mathf.FloorToInt(degrees);
		float minutes=60.0f*(degrees-fullDegrees);
		int fullMinutes=Mathf.FloorToInt(minutes);
		float seconds=60.0f*(minutes-fullMinutes);
        if (withSeconds && withSecondsDecimal)
            return System.String.Format("{0,3:F0}\u00B0{1:D2}'{2,4:F1}''", fullDegrees*sign, fullMinutes, seconds);
        else if (withSeconds)
            return System.String.Format("{0,3:F0}\u00B0{1:D2}'{2,2:F0}''", fullDegrees * sign, fullMinutes, seconds);
        else
            return System.String.Format("{0,3:F0}\u00B0{1,2:F0}'", fullDegrees * sign, minutes);
    }

    public static string HMSstring(float hours, bool withSeconds = true, bool withSecondsDecimal = true)
    {
        float sign = Mathf.Sign(hours);
        hours = Mathf.Abs(hours);
        int fullHours = Mathf.FloorToInt(hours);
        float minutes = 60.0f * (hours - fullHours);
        int fullMinutes = Mathf.FloorToInt(minutes);
        float seconds = 60.0f * (minutes - fullMinutes);
        if (withSeconds && withSecondsDecimal)
            return System.String.Format("{0,2:F0}h{1:D2}m{2,4:F1}s", fullHours * sign, fullMinutes, seconds);
        else if (withSeconds)
            return System.String.Format("{0,2:F0}h{1:D2}m{2,2:F0}s", fullHours * sign, fullMinutes, seconds);
        else
            return System.String.Format("{0,2:F0}h{1,2:F0}m", fullHours * sign, minutes);
    }


}