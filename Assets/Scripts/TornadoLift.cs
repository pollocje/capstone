using UnityEngine;

public class TornadoLift : MonoBehaviour
{
    [Header("Tornado Settings")]
    public float pullStrength = 15f;
    public float rotationStrength = 10f;
    public float liftStrength = 8f;
    public float maxVelocity = 10f; // This is the secret sauce

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float wanderRange = 18f;

    [Header("Extinguish Settings")]

    public string extinguisherTag = "Extinguish";
    public float extinguishTimer = 0f;


    // Picking a new point for movement
    private Vector3 startingPosition;
    private Vector3 targetPosition;

    [Header("Particle Growth")]
    public ParticleSystem tornadoParticles;
    public float maxParticleSize = 10f;
    public float growthDuration = 10f; // 10 seconds to reach full size

    private float growthTimer = 0f;

    void Start()
    {
        startingPosition = transform.position;
        PickNewTarget();
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

        // check if we have arrived, if so, choose a new direction
        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            PickNewTarget();
        }

        if (tornadoParticles != null && growthTimer < growthDuration)
        {
        growthTimer += Time.deltaTime;
        
        // Calculate how far along we are (0.0 to 1.0)
        float percent = growthTimer / growthDuration;
        
        // This targets the "Start Size" of the particles
        var main = tornadoParticles.main;
        main.startSizeMultiplier = Mathf.Lerp(1f, maxParticleSize, percent);
        }

    }

    void PickNewTarget()
    {
        float randomX = Random.Range(-wanderRange, wanderRange);
        float randomZ = Random.Range(-wanderRange, wanderRange);

        targetPosition = new Vector3(startingPosition.x + randomX, transform.position.y, startingPosition.z + randomZ);
    }

    void OnTriggerStay(Collider other)
    {
        Rigidbody rb = other.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            Vector3 centerPos = transform.position;
            Vector3 objectPos = other.transform.position;
            
            // 1. Directions
            Vector3 pullDir = (centerPos - objectPos);
            pullDir.y = 0;
            
            Vector3 swirlDir = Vector3.Cross(pullDir, Vector3.up);

            // 2. Apply Forces using VelocityChange (ignores mass for a consistent feel)
            // This makes the tornado feel "heavy" and authoritative
            rb.AddForce(pullDir.normalized * pullStrength * Time.fixedDeltaTime, ForceMode.VelocityChange);
            rb.AddForce(swirlDir.normalized * rotationStrength * Time.fixedDeltaTime, ForceMode.VelocityChange);
            rb.AddForce(Vector3.up * liftStrength * Time.fixedDeltaTime, ForceMode.VelocityChange);

            // 3. The Speed Limit - Prevents the "Cannon" effect
            if (rb.linearVelocity.magnitude > maxVelocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxVelocity;
            }

            // 4. Force higher drag to keep it from sliding out
            rb.linearDamping = 2f; 
            rb.angularDamping = 2f;

            // extinguish logic
            if (other.CompareTag(extinguisherTag))
            {
                extinguishTimer += Time.fixedDeltaTime;
                if (extinguishTimer >= 2f)
                {
                    Debug.Log("Tornado destroyed!");
                    Destroy(gameObject);
                }
            }
        }
    }

    // if item carrying extinguisher tag exits collider, reset timer
    void OnTriggerExit(Collider collider)
    {
        if (collider.CompareTag(extinguisherTag))
        {
            extinguishTimer = 0f;
        }
    }
}