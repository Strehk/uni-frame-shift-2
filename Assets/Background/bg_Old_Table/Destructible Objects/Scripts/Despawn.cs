using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Made by Rajendra Abhinaya, 2023

public class Despawn : MonoBehaviour
{
    private int despawnPercentage;
    private float despawnTime;
    private float distanceFromPlayer;
    private GameObject player;
    private AudioClip clip;
    private float volume;
    private float variation;
    private float volumeVariation;
    private float pitchVariation;
    private AudioSource audioSource;

    //Used to receive the variables' values from the parent object
    public void SetVariables(int despawnPercentage, float despawnTime, float distanceFromPlayer, GameObject player, AudioClip clip, float volume, float volumeVariation, float pitchVariation){
        this.despawnPercentage = despawnPercentage;
        this.despawnTime = despawnTime;
        this.distanceFromPlayer = distanceFromPlayer;
        this.player = player;
        this.clip = clip;
        this.volume = volume;
        this.volumeVariation = volumeVariation;
        this.pitchVariation = pitchVariation;
    }

    void Start(){
        //Plays a random audio clip from the list of audio clips set in the object
        audioSource = GetComponent<AudioSource>();
        audioSource.pitch = 1f + Random.Range(-pitchVariation/2, pitchVariation/2);
        audioSource.PlayOneShot(clip, volume + Random.Range(-volumeVariation, volumeVariation));
    }

    //Starts the selected despawn mode's coroutine function
    public void BeginCoroutine(string coroutine){
        switch(coroutine){
            case "Timed":
                StartCoroutine(DespawnCoroutine());
                break;
            case "Distance from Player":
                StartCoroutine(CheckDistance());
                break;
        }
    }

    //Despawns the debris based on the despawn percentage
    public void DespawnDebris(){
        int despawnCount = transform.childCount * despawnPercentage/100;
        for(int i = transform.childCount-1; i >= transform.childCount-despawnCount; i--){
            Destroy(transform.GetChild(i).gameObject);
        }
    }

    //Checks the distance between the debris and the player every 0.5 seconds after a 5 second delay
    public IEnumerator CheckDistance(){
        yield return new WaitForSeconds(5f);
        while(true){
            Vector3 distance = transform.position - player.transform.position;
            if(distance.magnitude > distanceFromPlayer){
                DespawnDebris();
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    //Despawns the debris after a set amount of time
    public IEnumerator DespawnCoroutine(){
        yield return new WaitForSeconds(despawnTime);
        DespawnDebris();
    }
}
