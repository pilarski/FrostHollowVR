/*
   Copyright 2021 Patrick Pilarski

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */

// This file is responsible for loading the experimental protocol
// that determines each unqiue set of session and trial parameters

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ProtocolManager : MonoBehaviour
{

    public EnvironmentFunction Environment;
    public int SessionId = 0;
    public int TrialId = 0;
    public int PredNum = 0;
    public string NoteTextToAppendtoFile = "";
    public string ProtocolFile = "Assets/Protocols/";

    // Start is called before the first frame update
    void Start()
    {
        ParseProtocol();
    }

    // Turn text params into environment conditions
    void ParseProtocol()
    {

        string current = "";
        List<string[]> conditions = new List<string[]>();

        using (var reader = new StreamReader(ProtocolFile))
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(',');
                conditions.Add(values);
            }
        }

        // Default to fixed condition with no assistant
        bool rand = false;
        bool drift = false;
        bool assist = false;
        int repNum = 0;
        float pulseTimeShift = 0f;

        current = conditions[SessionId][TrialId];
        pulseTimeShift = float.Parse(conditions[SessionId][TrialId + 9]);

        Debug.Log(pulseTimeShift);

        if (current.Contains("R"))
        {
            rand = true;
        }
        if (current.Contains("D"))
        {
            drift = true;
        }
        if (current.Contains("B"))
        {
            assist = true;
            repNum = 0;
        }
        if (current.Contains("T"))
        {
            assist = true;
            repNum = 1;
        }

        Environment.PulseRandomShift = rand;
        Environment.PulseDrift = drift;
        Environment.HapticsOn = assist;
        Environment.PredRepNumber = repNum;
        Environment.PredTypeNumber = PredNum;
        Environment.PulsePeriod += pulseTimeShift;

        Environment.FileAppendText = "-" + current + "-Session-" + SessionId.ToString() + "-Trial-" + TrialId.ToString() + "-" + NoteTextToAppendtoFile;

    }

}
