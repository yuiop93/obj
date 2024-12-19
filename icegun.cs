using StarterAssets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // 引入NavMesh命名空间
using BehaviorDesigner.Runtime;
public class icegun : MonoBehaviour
{
    public GameObject ShootCamGamObj;
    public GameObject crossGameobj;
    public Transform gunMuzzle; // 槍口位置
    [SerializeField] ParticleSystem inkParticle;
    public Material hitMaterial; // 指定的材質
    private Vector3 aimWorldPos;
    private StarterAssetsInputs _sai;
    private Animator _anim;

    private GameObject currentTarget; // 當前射中的物體
    private float targetHitTime = 0f; // 射中物體的持續時間
    public float requiredHitTime = 2f; // 需要射擊的時間 (秒)
    public bool isAiming = false;
    private Dictionary<GameObject, Material[]> originalMaterials = new Dictionary<GameObject, Material[]>(); // 保存原始材質
    // 音效相關
    public AudioSource aimAudioSource;      // 瞄準音效
    public AudioSource shootAudioSource;    // 射擊音效
    public AudioSource replaceMaterialAudioSource; // 替換材質音效

    private bool hasPlayedAimSound = false; // 瞄準音效標記
    private bool hasPlayedReplaceSound = false; // 替換材質音效標記

    void Start()
    {
        _sai = GetComponent<StarterAssetsInputs>();
        _anim = GetComponent<Animator>();
    }

    void Update()
    {
        // 更新瞄準點
        aimWorldPos = GetAimPoint();

        if (_sai.aim)
        {
            ShootCamGamObj.SetActive(true);
            crossGameobj.SetActive(true);
            _anim.SetLayerWeight(1, Mathf.Lerp(_anim.GetLayerWeight(1), 1f, Time.deltaTime * 10));
            isAiming = true;
            // 撥放瞄準音效（只播放一次）
            if (!hasPlayedAimSound)
            {
                aimAudioSource.PlayOneShot(aimAudioSource.clip);
                hasPlayedAimSound = true;
            }
            // 計算水平偏移後的目標方向
            Vector3 temp = aimWorldPos;
            temp.y = transform.position.y;
            Vector3 aimDirection = (temp - transform.position).normalized;
            transform.forward = Vector3.Lerp(transform.forward, aimDirection, Time.deltaTime * 50);

            if (Input.GetButton("Fire1")) // 按住開火
            {
                Shoot();
            }
            else
            {
                ResetHitTarget();
                shootAudioSource.Stop(); // 停止射擊音效
            }
        }
        else
        {
            inkParticle.Stop();
            ShootCamGamObj.SetActive(false);
            crossGameobj.SetActive(false);
            _anim.SetLayerWeight(1, Mathf.Lerp(_anim.GetLayerWeight(1), 0f, Time.deltaTime * 10));
            isAiming = false;
            hasPlayedAimSound = false;
            ResetHitTarget();
        }
    }

    private Vector3 GetAimPoint()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        if (Physics.Raycast(ray, out RaycastHit rh, 100f))
        {
            return rh.point;
        }
        return ray.origin + ray.direction * 100f;
    }

    private void Shoot()
    {
        // 循環播放射擊音效（但不重疊）
        if (!shootAudioSource.isPlaying)
        {
            shootAudioSource.Play();
        }
        // 計算從槍口到瞄準點的方向
        Vector3 shootDirection = (aimWorldPos - gunMuzzle.position).normalized;

        // 將 inkParticle 的位置設置在槍口
        inkParticle.transform.position = gunMuzzle.position;

        // 設置粒子系統的方向
        inkParticle.transform.rotation = Quaternion.LookRotation(shootDirection);

        // 播放粒子效果
        if (!inkParticle.isPlaying)
            inkParticle.Play();

        // 發射射線
        Ray ray = new Ray(gunMuzzle.position, shootDirection);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            GameObject hitObject = hit.collider.gameObject;

            if (currentTarget == hitObject)
            {
                // 持續射中同一物體
                targetHitTime += Time.deltaTime;

                if (targetHitTime >= requiredHitTime)
                {
                    ReplaceMaterial(hitObject);
                    ResetHitTarget();
                }
            }
            else
            {
                // 切換新目標
                currentTarget = hitObject;
                targetHitTime = 0f;
            }
        }
        else
        {
            ResetHitTarget();
        }
    }

    private void ResetHitTarget()
    {
        currentTarget = null;
        targetHitTime = 0f;
    }

    private void ReplaceMaterial(GameObject target)
    {
        
        // 播放替換材質音效（只播放一次）
        if (!hasPlayedReplaceSound)
        {
            replaceMaterialAudioSource.PlayOneShot(replaceMaterialAudioSource.clip);
            hasPlayedReplaceSound = true;
        }
        // 記錄目標物體及所有子物件的原始材質
        if (!originalMaterials.ContainsKey(target))
        {
            
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            Material[] materials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                materials[i] = renderers[i].material;
            }
            originalMaterials.Add(target, materials);
        }

        // 替換目標物體及所有子物件的材質
        Renderer[] targetRenderers = target.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in targetRenderers)
        {
            r.material = hitMaterial;
        }

        // 禁止目標物體的動畫 (Animator)
        Animator animator = target.GetComponent<Animator>();
        if (animator != null)
        {
            animator.enabled = false; // 禁用 Animator
            Debug.Log($"動畫已停止: {target.name}");
        }

        // 禁用 NavMeshAgent
        NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.ResetPath(); // 停止當前導航
            agent.velocity = Vector3.zero; // 確保物體停止移動
            agent.enabled = false; // 禁用導航
            Debug.Log($"NavMesh 已禁用: {target.name}");
        }

        // 禁用 Behavior Tree (Behavior Designer)
        BehaviorTree behaviorTree = target.GetComponent<BehaviorTree>();
        if (behaviorTree != null)
        {
            behaviorTree.enabled = false; // 禁用 Behavior Tree
            Debug.Log($"Behavior Tree 已禁用: {target.name}");
        }

        Debug.Log($"材質已替換並禁用組件: {target.name}");

        // 啟動協程，延遲恢復所有禁用的組件和材質
        StartCoroutine(RestoreComponentsAfterDelay(target, agent, behaviorTree, animator, 5f)); // 延遲5秒
    }

    private IEnumerator RestoreComponentsAfterDelay(GameObject target, NavMeshAgent agent, BehaviorTree behaviorTree, Animator animator, float delay)
    {
        yield return new WaitForSeconds(delay);
        hasPlayedReplaceSound = false; // 重置音效標記
        // 恢復材質
        if (originalMaterials.ContainsKey(target))
        {
            Renderer[] targetRenderers = target.GetComponentsInChildren<Renderer>();
            Material[] materials = originalMaterials[target];
            for (int i = 0; i < targetRenderers.Length && i < materials.Length; i++)
            {
                targetRenderers[i].material = materials[i];
            }
            originalMaterials.Remove(target); // 移除記錄，避免記憶體浪費
            Debug.Log($"材質已恢復: {target.name}");
        }

        // 恢復 Animator
        if (animator != null)
        {
            animator.enabled = true;
            Debug.Log($"動畫已恢復: {target.name}");
        }

        // 恢復 NavMeshAgent
        if (agent != null)
        {
            agent.enabled = true;
            Debug.Log($"NavMesh 已恢復: {target.name}");
        }

        // 恢復 Behavior Tree
        if (behaviorTree != null)
        {
            behaviorTree.enabled = true;
            Debug.Log($"Behavior Tree 已恢復: {target.name}");
        }

        Debug.Log($"所有組件和材質已恢復: {target.name}");
    }
}
