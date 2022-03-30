/*
   Copyright 2021 Patrick M. Pilarski

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

// This file is responsible for creating the experimental environment
// and also contains the learning code and logging code needed to
// run the Frost Hollow environment and associated co-agent.

/* This code was the basis for the VR experiments in the following manuscripts:

"The Frost Hollow Experiments: Pavlovian Signalling as a Path to
Coordination and Communication Between Agents,"
Patrick M. Pilarski, Andrew Butcher, Elnaz Davoodi, Michael Bradley Johanson,
Dylan J. A. Brenneis, Adam S. R. Parker, Leslie Acker, Matthew M. Botvinick,
Joseph Modayil, Adam White,
arXiv:2203.09498[cs.AI], 2022.

and

"Assessing Human Interaction in Virtual Reality With Continually Learning
Prediction Agents Based on Reinforcement Learning Algorithms: A Pilot Study"
Dylan J. A. Brenneis, Adam S. Parker, Michael Bradley Johanson, Andrew Butcher,
Elnaz Davoodi, Leslie Acker, Matthew M. Botvinick, Joseph Modayil,
Adam White, Patrick M. Pilarski,
arXiv:2112.07774[cs.AI], 2021.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using Valve.VR;

public class EnvironmentFunction : MonoBehaviour
{
    public string FileAppendText = "";
    public SteamVR_Action_Vibration HapticAction;
    public float ExperimentTime = 360f;
    public bool AdhereToExpTime = true;
    public bool PulseRandomShift = false;
    public bool PulseDrift = false;
    public bool HapticsOn = true;    
    public float PulseModifier = 0f;
    public float PulsePeriod = 5.0f;
    public float PulseLength = 1.0f;
    public float PulseRandLow = -2.0f;
    public float PulseRandHigh = 3.0f;
    public float PulseDriftLow = -1.0f;
    public float PulseDriftHigh = 1.0f;
    public float PulsePeriodMin = 4f;
    public float PulsePeriodMax = 6.5f;
    public float Points = 0.0f;
    public int BigPoints = 0;    
    public float RiftMinYPos = -10.0f;
    public float RiftMaxYPos = -3.00f;
    public float BloomMin = 0.0f;
    public float BloomMax = 1.2f;  
    public float ColShiftMin = 0.0f;
    public float ColShiftMax = 20f;       
    public float PointsMultiplier = 0.03f;
    public float HeatSpotRadius = 0.25f;
    public float HazardSpotRadius = 1.5f;
    public float TimeTickLength = 0.2f;
    public int PredRepNumber = 0;
    public int PredTypeNumber = 0;
    public float PredTokenThreshold = 0.2f;
    public GameObject Rift;
    public GameObject Motes;
    public AudioClip PulseSound;
    public AudioClip ShoutSound;  
    public AudioClip StartStopSound;
    public AudioClip Distractor1;
    public AudioClip Distractor2;
    public AudioClip Distractor3;
    public AudioClip Distractor4;
    public AudioClip Distractor5;
    public AudioClip Distractor6;
    public AudioClip Distractor7;
    public GameObject BigpointController;
    public float BigpointControllerY;

    private string LogPath;
    private string LogPathW;    

    private float _timer = 0f;
    private float _pulseTimer = 0f;
    private int _pulseCount = 0;
    private bool _tick;
    private bool _pulsing;
    private bool _wasPulsing;
    private bool _pulseFallingEdge = false;
    private float _pup;
    private float _pus;    
    private float _pChange;
    private float _riftX;
    private float _riftY;   
    private float _riftZ;
    private float _dist;
    private float _lastDist;
    private float _distX;
    private float _distY;   
    private float _distZ;
    private float _threshCrossIn;
    private float _threshCrossOut;
    private float _bloomThresh;
    private float _hShift;
    private ParticleSystem _ps; 
    private StreamWriter _logWriter = null;    
    private Bloom _bloomLayer = null;   
    private ColorGrading _colorGradingLayer = null;
    private AudioClip[] _distractors;

    private static int numAgents = 4;
    private static int numWeightsPerAgent = 2;    
    private static int _stateLen = 61;
    private float[,] _stateTp1 = new float[numAgents,_stateLen];
    private float[,] _stateT = new float[numAgents,_stateLen];   
    // public float[] StateT = new float[_stateLen];  
    private float[,,] _weight = new float[numAgents,numWeightsPerAgent,_stateLen];
    private float[,,] _etrace = new float[numAgents, numWeightsPerAgent, _stateLen];
    private float _alpha = 0.1f;
    private float _r = 0.0f;   
    private float _gamma = 0.99f;
    private float _delta = 0f;
    private float _lambda = 0.99f;
    private float[,] _pred = new float[numAgents, numWeightsPerAgent];
    private int[] _idx = new int[numAgents];
    private float[] _idxF = new float[numAgents];    

    // Start is called before the first frame update
    void Start()
    {
        LogPath = "Assets/Logs/FrostHollow-log" + FileAppendText + ".csv";
        LogPathW = "Assets/Logs/FrostHollow-log-weights" + FileAppendText + ".csv";
        Points = 0.0f;
        BigPoints = 0;
        _riftX = Rift.transform.position.x;
        _riftY = RiftMaxYPos;   
        _riftZ = Rift.transform.position.z;        
        Rift.transform.position = new Vector3(_riftX,_riftY,_riftZ);
        _ps = Motes.GetComponent<ParticleSystem>();
        PostProcessVolume volume = gameObject.GetComponent<PostProcessVolume>();
        volume.profile.TryGetSettings(out _bloomLayer);
        volume.profile.TryGetSettings(out _colorGradingLayer);
        _colorGradingLayer.enabled.value = true;     
        _logWriter = new StreamWriter(LogPath, false);

        string to = "";
        to += "Time, deltaTime, pulseTime, pulseCount, intervalWithPulse, intervalWithoutPulse, BigPoints, Points, pChange, dist, dX, dY, dZ, pulsing,";
        to += "threshCrossIn, threshCrossOut,";
        for (int n = 0; n < numAgents; n++)
        {
            to += "X" + n.ToString() + "q0, ";
            to += "P" + n.ToString() + "q0, ";
            to += "X" + n.ToString() + "q1, ";
            to += "P" + n.ToString() + "q1, ";
        }
        _logWriter.WriteLine(to);  
        Points = RiftMinYPos;
        _idxF[0] = 0.0f;        
        _idxF[1] = 1.0f;
        _pup = PulsePeriod + PulseModifier;
        _pus = _pup - PulseLength;
        AudioSource.PlayClipAtPoint(StartStopSound,transform.position);
        _distractors = new AudioClip[] { Distractor1, Distractor2, Distractor3, Distractor4, Distractor5, Distractor6, Distractor7 };
        Invoke("CheckDistractorAudio", UnityEngine.Random.Range(0, PulsePeriod * 0.5f));
    }

    // Update is called once per frame
    void Update()
    {
        _tick = false;
        _timer += Time.deltaTime;
        _pulseTimer += Time.deltaTime;        
        if (_timer > TimeTickLength)
        {
            _timer = 0f;
            _tick = true;
        }        
        _dist = Vector3.Distance(new Vector3(0,transform.position.y,0), transform.position);
        _pChange = 0.0f;
        if (_dist < HeatSpotRadius) {
            _pChange += PointsMultiplier * 1.0f;
        }
        // Check for moving in and out of points gaining threshold region
        _threshCrossIn = 0f;
        _threshCrossOut = 0f;
        if (_dist < HeatSpotRadius && _lastDist >= HeatSpotRadius)
        {
            _threshCrossIn = _pulseTimer;
        }
        if (_dist >= HeatSpotRadius && _lastDist < HeatSpotRadius)
        {
            _threshCrossOut = _pulseTimer;
        }
        _pChange += CheckPulse();        
        Points += _pChange;
        var emission = _ps.emission;
        if (_pChange < 0.0f) {
            emission.enabled = false;
            _bloomThresh = Mathf.Clamp(_bloomThresh - 0.1f,BloomMin,BloomMax);
            _hShift = Mathf.Clamp(_hShift + 10f,ColShiftMin,ColShiftMax);                                     
        }
        else {
            emission.enabled = true;
            _bloomThresh = Mathf.Clamp(_bloomThresh + 0.01f,BloomMin,BloomMax);
            _hShift = Mathf.Clamp(_hShift - 1f,ColShiftMin,ColShiftMax);               
        }
        if (_pChange == 0.0f)
        {
            emission.enabled = false;
        }
        _bloomLayer.threshold.value = _bloomThresh;
        _bloomLayer.diffusion.value = (1.2f - _bloomThresh)*3.0f+5.7f; 
        _bloomLayer.intensity.value = (1.2f - _bloomThresh)*4.0f+2f;         
        _colorGradingLayer.hueShift.value = _hShift;
        _colorGradingLayer.postExposure.value = (1.2f - _bloomThresh)*1.0f; 
        Points = Mathf.Clamp(Points,RiftMinYPos,RiftMaxYPos);
        _riftY = Points;        
        Rift.transform.position = new Vector3(_riftX,_riftY,_riftZ);
        AgentStep();
        CheckBigPoints();
        _lastDist = _dist;
        string textOut = "";
        textOut += (Time.timeSinceLevelLoad).ToString("#0.0000") + "," + Time.deltaTime.ToString("#0.0000") + "," + _pulseTimer.ToString("#0.0000") + "," + _pulseCount.ToString() + "," + _pup.ToString("#0.0000") + "," + _pus.ToString("#0.0000") + ",";
        textOut += (BigPoints).ToString()+","+(Points+3.0f).ToString("#.0000")+","+_pChange.ToString("#.0000")+","+_dist.ToString("#.0000")+","+_distX.ToString("#.0000")+","+_distY.ToString("#.0000")+","+_distZ.ToString("#.0000")+","+Convert.ToInt32(_pulsing).ToString()+",";
        textOut += _threshCrossIn.ToString("#0.0000") + "," + _threshCrossOut.ToString("#0.0000") + ",";
        for (int n = 0; n < numAgents; n++)
        {
            textOut += _idx[n].ToString() + "," + _pred[n,0].ToString("#.0000")+",";
            textOut += _idx[n].ToString() + "," + _pred[n,1].ToString("#.0000") + ",";
        }
        if (AdhereToExpTime) {
            if (Time.timeSinceLevelLoad < ExperimentTime) {
                _logWriter.WriteLine(textOut);
                _logWriter.Flush();
            }
            else {
                if (_alpha > 0){
                    AudioSource.PlayClipAtPoint(StartStopSound,transform.position);
                }
                 _bloomThresh = BloomMin;
                 _hShift = ColShiftMax;
                 _alpha = 0f; // Stop learning
            }
        }
        else{
            _logWriter.WriteLine(textOut);
            _logWriter.Flush();       
        }
    }

    bool CheckDistractorAudio()
    {
        if (true)
        {
            AudioClip clip = _distractors[UnityEngine.Random.Range(0, _distractors.Length)];
            float volume = UnityEngine.Random.Range(1f, 4f);
            Vector3 loc = transform.position + new Vector3(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(1f, 3f), UnityEngine.Random.Range(-5f, 5f));
            AudioSource.PlayClipAtPoint(clip, loc, volume);
        }
        return true;
    }


        float CheckPulse()
    {
        _wasPulsing = _pulsing;
        float pointDelta = 0.0f; 
        _distX = Vector3.Distance(new Vector3(0,transform.position.y,transform.position.z), transform.position);
        _distY = Vector3.Distance(new Vector3(transform.position.x,0,transform.position.z), transform.position);
        _distZ = Vector3.Distance(new Vector3(transform.position.x,transform.position.y,0), transform.position);       
        if (_pulseTimer % _pup > _pus ) {
            if (!_pulsing) {
                AudioSource.PlayClipAtPoint(PulseSound,transform.position);
            }                   
            _pulsing = true;
            if (_distX < HazardSpotRadius) { 
                pointDelta = -0.2f;
            }
        }
        else {
            if (_pulsing)
            {
                _pulseTimer = 0f;
                _pulseCount += 1;
                if (PulseRandomShift)
                {
                    PulseModifier = UnityEngine.Random.Range(PulseRandLow, PulseRandHigh);
                }
                if (PulseDrift)
                {
                    PulsePeriod = Mathf.Clamp(PulsePeriod += UnityEngine.Random.Range(PulseDriftLow, PulseDriftHigh), PulsePeriodMin, PulsePeriodMax);
                }
                _pup = PulsePeriod + PulseModifier;
                _pus = _pup - PulseLength;        
            }
            _pulsing = false;
        }

        // Check for a falling edge
        if (_wasPulsing && !_pulsing)
        {
            _pulseFallingEdge = true;
            Invoke("CheckDistractorAudio", UnityEngine.Random.Range(0, PulsePeriod*0.7f));
            Invoke("CheckDistractorAudio", UnityEngine.Random.Range(0, PulsePeriod * 0.33f));
        }
        else
        {
            _pulseFallingEdge = false;
        }

        return pointDelta;
    }

    int CheckBigPoints()
    {
        var trackedObject = BigpointController.GetComponent<SteamVR_TrackedObject>();
        BigpointControllerY = trackedObject.transform.position.y;
        if (transform.position.y < BigpointControllerY) {
            if (Points == RiftMaxYPos && _dist < HeatSpotRadius) {
                BigPoints += 1;
                Points = RiftMinYPos;
                AudioSource.PlayClipAtPoint(ShoutSound,transform.position);
                _bloomThresh = 0.0f;     
                return 1;           
            }
        }
        return 0;
    }

    void OnDestroy() {
         _logWriter.Close();

        StreamWriter logWriter = new StreamWriter(LogPathW, false);        
        for (int n = 0; n < numAgents; n++)
        {
            for (int w = 0; w < numWeightsPerAgent; w++)
            {
                string textOut = "";
                for (int i = 0; i < _stateLen; i++)
                {                        
                    textOut += _weight[n,w,i].ToString()+",";
                }
                logWriter.WriteLine(textOut); 
            }
        }
        logWriter.Close();

    }

    public float DebugSignal {
        get { return (float)BigPoints; }
        private set {}
    }


    public float[,] DebugAgentStateVector {
        get { return _stateT; }
        private set {}
    }

    public float[,,] DebugAgentWeightVector {
        get { return _weight; }
        private set {}
    }    

    float AgentStep() {

        // --- New State Vector ---
        _stateTp1 = new float[numAgents,_stateLen];

        float[] rho = new float[] { 1.0f, 1.0f };

        //--- Agent 0: Linear Bit Stepper ---

        float[] newRho = new float[] { 1.0f, 1.0f };
        // Create State
        float gammaCd = 1.0f;       
        int n = 0;
        if (_pulsing) {
            _r = 1f;
            gammaCd = 0f;
        }
        else {
            _r = 0f;
        }
        if (_tick)
        {
            _idxF[n] += 1f;
        }
        if (_pulseFallingEdge)
        {
            _idxF[n] = 0f;
        }
        _idx[n] = (int)_idxF[n];
        if (_idx[n] >= _stateLen) {
            _idx[n] = _stateLen - 1;
        } 
       _stateTp1[n,_idx[n]] = 1;

        AgentTdStep(n, _r, gammaCd, newRho);

        //--- Agent 1: Exponential Trace ---

        // Create State
        gammaCd = 1.0f;
        n = 1;
        _idxF[n] *= 0.998f;
        if (_pulseFallingEdge)
        {
            _idxF[n] = 1.0f;
        }
        if (_pulsing) {
            _r = 1f;
            gammaCd = 0f;
        }
        else {
            _r = 0f;
        }
        _idx[n] = 40 - (int)(_idxF[n]*40f);
        if (_idx[n] >= _stateLen) {
            _idx[n] = _stateLen - 1;
        } 
       _stateTp1[n,_idx[n]] = 1;

        AgentTdStep(n, _r, gammaCd, rho);




        //--- Agent 2: Osscilating Clock (no stimulus) ---

        // Create State
        gammaCd = 1.0f;       
        n = 2;
        if (_pulsing) {
            if (_tick) {            
                _idxF[n] += 1f;
            }
            _r = 1f;
            gammaCd = 0f;
        }
        else {
            if (_tick) {
                _idxF[n] += 1f;
            }
            _r = 0f;
        }
        _idx[n] = (int)_idxF[n];
        if (_idxF[n] > 23.5) {
            _idx[n] = 0;
            _idxF[n] = 0f;
        } 
       _stateTp1[n,_idx[n]] = 1;

        AgentTdStep(n, _r, gammaCd, rho);


        //--- Agent 3: Bias Unit ---

        // Create State
        gammaCd = 1.0f;
        n = 3;
        if (_pulsing)
        {
            _idxF[n] = 0f;
            _r = 1f;
            gammaCd = 0f;
        }
        else
        {
            _idxF[n] = 0;
            _r = 0f;
        }
        _idx[n] = (int)_idxF[n];
        if (_idx[n] >= _stateLen)
        {
            _idx[n] = _stateLen - 1;
        }
        _stateTp1[n, _idx[n]] = 1;

        AgentTdStep(n, _r, gammaCd, rho);


        // --- Update and feedback ---
        _stateT = _stateTp1;
        if (HapticsOn) {
            if (_pred[PredRepNumber,PredTypeNumber] > PredTokenThreshold)
            HapticAction.Execute(0, 1f, 150f, 7.5f, SteamVR_Input_Sources.RightHand);
        }
        if (_pulsing)
        {
            HapticAction.Execute(0, 1f, 150f, 7.5f, SteamVR_Input_Sources.LeftHand);
        }
        return _pred[0,0];
    }

    void AgentTdStep(int n, float c, float gammaC, float[] rho)
    {
        // Fixed Gamma
        int m = 0;
        float sumT = 0f;
        float sumTp1 = 0f;
        for (int i = 0; i < _stateLen; i++)
        {
            if (_stateT[n, i] > 0)
            {
                _etrace[n, m, i] = 1f;
            }
        }
        for (int i = 0; i < _stateLen; i++){
            sumT += _weight[n,m,i] * _stateT[n,i];
            sumTp1 += _weight[n,m,i] * _stateTp1[n,i];     
        }
        var P = sumT;
        _delta = c + _gamma * sumTp1 - sumT;
        for (int i = 0; i < _stateLen; i++){
            _weight[n,m,i] += _alpha * _delta * _etrace[n, m, i] * rho[0];          
        }
        for (int i = 0; i < _stateLen; i++)
        {
            _etrace[n, m, i] = _lambda * _gamma * _etrace[n, m, i];
        }

        _pred[n, 0] = P;

        // Countdown Gamma
        m = 1;
        sumT = 0f;
        sumTp1 = 0f;
        for (int i = 0; i < _stateLen; i++)
        {
            if (_stateT[n, i] > 0)
            {
                _etrace[n, m, i] = 1f;
            }
        }
        for (int i = 0; i < _stateLen; i++){
            sumT += _weight[n,m,i] * _stateT[n,i];
            sumTp1 += _weight[n,m,i] * _stateTp1[n,i];     
        }
        P = sumT;
        _delta = 1.0f + gammaC * sumTp1 - sumT;
        for (int i = 0; i < _stateLen; i++){
            _weight[n,m,i] += _alpha * _delta * _etrace[n, m, i] * rho[1];            
        }
        for (int i = 0; i < _stateLen; i++)
        {
            _etrace[n, m, i] = _lambda * gammaC * _etrace[n, m, i];
        }
        _pred[n, 1] = P;
    }


}
