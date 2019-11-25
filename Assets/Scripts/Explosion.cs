﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Explosion : MonoBehaviour
{

    [SerializeField]
    ParticleSystem expl;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (!expl.isEmitting) {
            Destroy(this.gameObject);
        }
    }
}
