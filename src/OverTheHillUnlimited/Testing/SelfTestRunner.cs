using System;
using System.Collections.Generic;
using UnityEngine;

namespace OverTheHillUnlimited.Testing;

/// <summary>
/// Drives the self-test script one step per frame, honouring its Wait and Until pauses. Added by Plugin only when the
/// game was launched with -otu-selftest.
/// </summary>
public class SelfTestRunner : MonoBehaviour
{
    SelfTest _test;
    IEnumerator<object> _script;
    float _resumeAt;
    Until _until;
    float _untilDeadline;

    public SelfTestRunner(IntPtr pointer) : base(pointer)
    {
    }

    void Update()
    {
        if (_script == null)
        {
            if (_test != null)
                return;
            _test = new SelfTest(BepInEx.Logging.Logger.CreateLogSource("OTU Self-test"));
            _script = _test.Run();
        }

        float now = Time.unscaledTime;
        if (_until != null)
        {
            bool met;
            try
            {
                met = _until.Condition();
            }
            catch (Exception)
            {
                met = false;
            }
            if (!met)
            {
                if (now < _untilDeadline)
                    return;
                _test.TimedOut(_until.What);
                _script = null;
                return;
            }
            _until = null;
        }
        if (now < _resumeAt)
            return;

        try
        {
            if (!_script.MoveNext())
            {
                _script = null;
                return;
            }
        }
        catch (Exception e)
        {
            _test.Crashed(e);
            _script = null;
            return;
        }

        switch (_script.Current)
        {
            case Wait wait:
                _resumeAt = now + wait.Seconds;
                break;
            case Until until:
                _until = until;
                _untilDeadline = now + until.TimeoutSeconds;
                break;
        }
    }
}
