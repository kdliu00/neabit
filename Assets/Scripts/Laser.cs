﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class Laser : NetworkBehaviour
{
    public float damage;
    public float laserSpeed;
    public bool tracking = false;
    public GameObject trackedTarget;
    
    [SyncVar]
    public string owner;

    private Rigidbody rb;
    private float lifeTime;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        lifeTime = 3.0f;
    }

    // Update is called once per frame
    void Update()
    {
        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0) {
            Destroy(this.gameObject);
        }
        RaycastHit hit;
        if (Physics.Raycast(rb.position - (transform.forward * 4), transform.forward, 
                                out hit, rb.velocity.magnitude * Time.fixedDeltaTime)) {
            Transform tr = hit.transform;
            if (tr.tag == "Player") {
                if (tr.gameObject.GetComponent<PlaneControl>()._ID != owner) {
                    tr.gameObject.GetComponent<PlaneControl>().CmdPlayerShot(damage, this.gameObject);
                    Debug.Log("Damaged " + tr.gameObject.GetComponent<PlaneControl>()._ID);
                }
            } else {
                CmdDestroy(this.gameObject);
            }
        }
    }

    void FixedUpdate() {
        if (tracking) {
            rb.velocity = Vector3.Normalize(trackedTarget.transform.position - transform.position)
                             * laserSpeed;
        } else {
            rb.velocity = transform.forward * laserSpeed;
        }
        RpcUpdate(rb.velocity);
    }

    [ClientRpc]
    void RpcUpdate(Vector3 rbvel) {
        rb.velocity = rbvel;
    }

    [Command]
    public void CmdDestroy(GameObject go) {
        NetworkServer.Destroy(go);
    }
}
