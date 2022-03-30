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

// This file is responsible for displaying learning variables,
// parameters, and signals as a tangible object and/or projector
// in visual form to the user in virtual reality (VR)

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebuggingCrystal : MonoBehaviour
{

    public EnvironmentFunction Target;
    public float SigMin = -10f;
    public float SigMax = -3f;
    public float WMin = -0.1f;
    public float WMax = 0.1f;    
    public int StateLength = 10;
    public int NumAgents = 1;
    public int NumWeightsPerAgent = 2;    
    public GameObject Bit;
    public bool ProjectState = true;   
    public float TrackedSignal = 0f;    
    public bool ClampSignal = true;

    private GameObject[,] _bitArrayS;
    private GameObject[,,] _bitArrayW;        

    private Transform _orb;
    private Material _mat;
    private Color _onColor;
    private Color _onEmmColor;    
    private Color _offColor;    

    // Start is called before the first frame update
    void Start()
    {
        _orb = this.gameObject.transform.GetChild(1);
        _mat = _orb.GetComponent<Renderer>().material;
        _onColor = _mat.color;
        _offColor = Color.black;
        _onEmmColor = _mat.GetColor ("_EmissionColor");
        if (ProjectState) {    
            MakeProjector();
        }        
    }

    // Update is called once per frame
    void Update()
    {
        float sig = Target.DebugSignal;
        TrackedSignal = sig;
        if (ClampSignal) {
            sig = Mathf.Clamp(sig, SigMin, SigMax);
        }
        float lerp = (sig - SigMin) / (SigMax - SigMin);
        _mat.color = Color.Lerp(_offColor, _onColor, lerp);   
        _mat.SetColor ("_EmissionColor",  Color.Lerp(_offColor, _onEmmColor, lerp));
        if (ProjectState) {         
            Project();
        }
    }

    void MakeProjector()
    {
        _bitArrayS = new GameObject[NumAgents, StateLength];
        _bitArrayW = new GameObject[NumAgents, NumWeightsPerAgent, StateLength]; 
        for (int n = 0; n < NumAgents; n++)
        {                         
            for (int i = 0; i < StateLength; i++)
            {
                GameObject gos = Instantiate(Bit, new Vector3((float)i*0.1f - 0.05f*(float)StateLength, 1f + 0.4f*(float)n, 1f), Quaternion.identity, this.gameObject.transform) as GameObject;
                gos.transform.localScale = new Vector3(1f,1f,1f);
                _bitArrayS[n,i] = gos;

                for (int m = 0; m < NumWeightsPerAgent; m++)
                {
                    GameObject gow = Instantiate(Bit, new Vector3((float)i*0.1f - 0.05f*(float)StateLength, 0.9f + 0.4f*(float)n- 0.1f*(float)m, 1f), Quaternion.identity, this.gameObject.transform) as GameObject;
                    gow.transform.localScale = new Vector3(1f,1f,1f);            
                    _bitArrayW[n,m,i] = gow; 
                }           
            } 
        } 
    }

    void Project()
    {
        float[,] s = Target.DebugAgentStateVector;
        float[,,] w = Target.DebugAgentWeightVector;
        for (int n = 0; n < NumAgents; n++)
        { 
            for (int i = 0; i < StateLength; i++)
            {
                    Material ms = _bitArrayS[n,i].GetComponent<Renderer>().material;
                    ms.color = Color.Lerp(_offColor, _onColor, s[n,i]);
                    ms.SetColor ("_EmissionColor",  Color.Lerp(_offColor, _onEmmColor, s[n,i])); 
                    for (int m = 0; m < NumWeightsPerAgent; m++)
                    {            
                        Material mw = _bitArrayW[n,m,i].GetComponent<Renderer>().material;                    
                        float lerp = (w[n,m,i] - WMin) / (WMax - WMin);
                        mw.color = Color.Lerp(_offColor, _onColor, lerp); 
                        mw.SetColor ("_EmissionColor",  Color.Lerp(_offColor, _onEmmColor, lerp));   
                    }                                   
            }
        }          
    }

    void OnDestroy()
    {
        for (int n = 0; n < NumAgents; n++)
        {         
            for (int i = 0; i < StateLength; i++)
            {
                Destroy(_bitArrayS[n,i]);
                for (int m = 0; m < NumWeightsPerAgent; m++)
                {                        
                    Destroy(_bitArrayW[n,m,i]);
                }            
            }
        }
    }

}
