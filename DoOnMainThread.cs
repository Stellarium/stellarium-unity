/* DoOnMainThread
 * Helper class for the Stellarium Unity Bridge 
 * This script (c) 2016 Neil Zehr & David Rodriguez (IDIA Lab)
 *
 * 
 *  MISSING DOCUMENTATION
 *  Purpose of this script? Efficiency gained?
 * 
 */

using UnityEngine;
using System.Collections.Generic;
using System;

public class DoOnMainThread : MonoBehaviour {

    public readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();

    public virtual void Update() {
        // dispatch stuff on main thread
        while(ExecuteOnMainThread.Count > 0) {
            ExecuteOnMainThread.Dequeue().Invoke();
        }
    }
}
