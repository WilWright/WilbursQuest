using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ParticlePooler : MonoBehaviour {
    public class Particle {
        public GameObject particleObject;
        public ParticleSystem particleSystem;

        public Particle(GameObject particleObject) {
            this.particleObject = particleObject;
            particleObject.SetActive(false);
            particleSystem = particleObject.GetComponent<ParticleSystem>();
        }
    }

    public GameObject[] initialPools;
    public int[] initialAmounts;
    public Dictionary<string, Queue<Particle>> particles = new Dictionary<string, Queue<Particle>>();
    public List<Particle> activeParticles = new List<Particle>();

    public void Init() {
        for (int i = 0; i < initialPools.Length; i++)
            CreatePool(initialPools[i], initialAmounts[i]);
    }

    public void CreatePool(GameObject template, int amount) {
        string name = template.name;
        particles.Add(name, new Queue<Particle>());
        GameObject holder = new GameObject(name + "Holder");

        for (int i = 0; i < amount; i++) {
            GameObject go = Instantiate(template);
            go.name = name;
            go.transform.SetParent(holder.transform);
            particles[name].Enqueue(new Particle(go));
        }

        holder.transform.SetParent(template.transform);
    }

    public Particle GetParticle(string name) {
        Queue<Particle> queue = particles[name];
        return queue.Count > 0 ? queue.Dequeue() : null;
    }
    public void PlayParticle(Particle particle, int count = 0) {
        activeParticles.Add(particle);
        particle.particleObject.SetActive(true);
        if (count > 0) particle.particleSystem.Emit(count);
        else           particle.particleSystem.Play();
        StartCoroutine(iePlayParticle(particle));
    }
    IEnumerator iePlayParticle(Particle particle) {
        yield return new WaitWhile(() => particle.particleSystem.IsAlive());

        particle.particleObject.SetActive(false);
        particles[particle.particleObject.name].Enqueue(particle);
        activeParticles.Remove(particle);
    }

    public void ResetParticles() {
        StopAllCoroutines();
        foreach (Particle p in activeParticles) {
            p.particleObject.SetActive(false);
            particles[p.particleObject.name].Enqueue(p);
        }
        activeParticles = new List<Particle>();
    }
}
