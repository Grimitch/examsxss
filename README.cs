using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class TowerDefenseUltimate : MonoBehaviour
{
    public static TowerDefenseUltimate I;

    public int money = 200;
    public int lives = 20;
    public int score = 0;

    public Text moneyText;
    public Text livesText;
    public Text scoreText;
    public Image fadePanel;

    public GameObject fastEnemy;
    public GameObject tankEnemy;
    public GameObject healerEnemy;
    public GameObject invisibleEnemy;

    public GameObject towerFast;
    public GameObject towerStrong;
    public GameObject towerAOE;

    public GameObject bulletPrefab;
    public GameObject aoeExplosionPrefab;

    public GameObject hitFX;
    public GameObject explosionFX;

    public AudioSource audioSource;
    public AudioClip shootSound;
    public AudioClip hitSound;
    public AudioClip explosionSound;

    public Transform[] enemyPath;

    public List<Wave> waves = new List<Wave>();
    public float initialDelay = 3f;

    public bool playSounds = true;

    int currentWave = 0;

    public static List<Enemy> allEnemies = new();
    public static List<Tower> allTowers = new();

    void Awake()
    {
        if (I == null) I = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        UpdateUI();
        StartCoroutine(FadeIn());
        StartCoroutine(StartWaves());
    }

    public void AddMoney(int amount)
    {
        money += amount;
        score += amount;
        UpdateUI();
    }

    public bool SpendMoney(int amount)
    {
        if (money < amount) return false;
        money -= amount;
        UpdateUI();
        return true;
    }

    public void DamageLife(int x)
    {
        lives -= x;
        UpdateUI();
        if (lives <= 0) GameOver();
    }

    void UpdateUI()
    {
        if (moneyText) moneyText.text = "💰 " + money;
        if (livesText) livesText.text = "❤️ " + lives;
        if (scoreText) scoreText.text = "⭐ " + score;
    }

    IEnumerator FadeIn()
    {
        if (!fadePanel) yield break;

        Color c = fadePanel.color;
        for (float t = 1; t > 0; t -= 0.03f)
        {
            c.a = t;
            fadePanel.color = c;
            yield return null;
        }
    }

    IEnumerator FadeOut(string scene)
    {
        if (!fadePanel)
        {
            SceneManager.LoadScene(scene);
            yield break;
        }

        Color c = fadePanel.color;
        for (float t = 0; t < 1; t += 0.03f)
        {
            c.a = t;
            fadePanel.color = c;
            yield return null;
        }

        SceneManager.LoadScene(scene);
    }

    void GameOver()
    {
        int best = PlayerPrefs.GetInt("BestScore", 0);
        if (score > best) PlayerPrefs.SetInt("BestScore", score);
        StartCoroutine(FadeOut("GameOver"));
    }

    IEnumerator StartWaves()
    {
        yield return new WaitForSeconds(initialDelay);
        currentWave = 0;

        while (currentWave < waves.Count)
        {
            Wave w = waves[currentWave];

            for (int i = 0; i < w.count; i++)
            {
                SpawnEnemy(w.enemyType, w.health, w.speed, w.reward);
                yield return new WaitForSeconds(w.spawnRate);
            }

            currentWave++;
            yield return new WaitForSeconds(4f);
        }
    }

    public void SpawnEnemy(EnemyType type, float hp, float sp, int reward)
    {
        GameObject prefab = fastEnemy;
        switch (type)
        {
            case EnemyType.Fast: prefab = fastEnemy; break;
            case EnemyType.Tank: prefab = tankEnemy; break;
            case EnemyType.Healer: prefab = healerEnemy; break;
            case EnemyType.Invisible: prefab = invisibleEnemy; break;
        }

        if (!prefab || enemyPath.Length == 0) return;

        var eObj = Instantiate(prefab, enemyPath[0].position, Quaternion.identity);
        var e = eObj.AddComponent<Enemy>();
        e.Init(hp, sp, reward, enemyPath, type);
        allEnemies.Add(e);
    }

    public void PlayOneShot(AudioClip clip)
    {
        if (!playSounds || !audioSource || !clip) return;
        audioSource.PlayOneShot(clip);
    }

    [System.Serializable]
    public class Wave
    {
        public EnemyType enemyType = EnemyType.Fast;
        public int count = 5;
        public float health = 50f;
        public float speed = 1.5f;
        public int reward = 5;
        public float spawnRate = 0.8f;
    }

    public enum EnemyType { Fast, Tank, Healer, Invisible }

    public class Enemy : MonoBehaviour
    {
        float hp;
        float speed;
        int reward;
        Transform[] path;
        int index;
        EnemyType type;
        float healTimer;
        SpriteRenderer sr;
        float maxHp;

        public void Init(float h, float s, int r, Transform[] p, EnemyType t)
        {
            hp = h;
            speed = s;
            reward = r;
            path = p;
            type = t;
            maxHp = h;

            sr = GetComponent<SpriteRenderer>();
            if (sr && type == EnemyType.Invisible)
            {
                Color c = sr.color;
                c.a = 0.3f;
                sr.color = c;
            }
        }

        void Update()
        {
            Move();
            if (type == EnemyType.Healer) HealNearby();
        }

        void Move()
        {
            var target = path[index];
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, target.position) < 0.1f)
            {
                index++;
                if (index >= path.Length)
                {
                    TowerDefenseUltimate.I.DamageLife(1);
                    TowerDefenseUltimate.allEnemies.Remove(this);
                    Destroy(gameObject);
                }
            }
        }

        void HealNearby()
        {
            healTimer += Time.deltaTime;
            if (healTimer < 2f) return;
            healTimer = 0;

            foreach (var e in allEnemies)
            {
                if (!e) continue;
                if (Vector3.Distance(transform.position, e.transform.position) < 2f)
                {
                    e.hp = Mathf.Min(e.hp + 10, e.maxHp);
                }
            }
        }

        public void Hit(float dmg)
        {
            hp -= dmg;
            if (hp <= 0) Die();
        }

        void Die()
        {
            Instantiate(TowerDefenseUltimate.I.hitFX, transform.position, Quaternion.identity);
            TowerDefenseUltimate.I.AddMoney(reward);
            TowerDefenseUltimate.allEnemies.Remove(this);
            Destroy(gameObject);
        }
    }

    public enum TowerType { Fast, Strong, AOE }

    public class Tower : MonoBehaviour
    {
        public float range = 3f;
        public float damage = 15f;
        public float fireRate = 1f;
        public int upgradeCost = 50;
        public TowerType towerType;

        float timer;

        void OnEnable() => allTowers.Add(this);
        void OnDisable() => allTowers.Remove(this);

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= fireRate)
            {
                var target = GetNearestEnemy();
                if (target) Attack(target);
                timer = 0;
            }
        }

        Enemy GetNearestEnemy()
        {
            Enemy best = null;
            float bestD = range + 1;

            foreach (var e in allEnemies)
            {
                if (!e) continue;
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < bestD && d <= range)
                {
                    best = e;
                    bestD = d;
                }
            }

            return best;
        }

        void Attack(Enemy e)
        {
            var mgr = TowerDefenseUltimate.I;

            if (towerType != TowerType.AOE)
            {
                var b = Instantiate(mgr.bulletPrefab, transform.position, Quaternion.identity)
                    .AddComponent<Bullet>();
                b.Init(e, damage);
            }
            else
            {
                Instantiate(mgr.explosionFX, e.transform.position, Quaternion.identity);
                foreach (var en in allEnemies)
                {
                    if (!en) continue;
                    if (Vector3.Distance(e.transform.position, en.transform.position) < 2.5f)
                        en.Hit(damage);
                }
            }

            mgr.PlayOneShot(mgr.shootSound);
        }

        public void Upgrade()
        {
            if (!TowerDefenseUltimate.I.SpendMoney(upgradeCost)) return;
            damage *= 1.5f;
            range += 0.7f;
            fireRate *= 0.8f;
            upgradeCost += 60;
        }
    }

    public class Bullet : MonoBehaviour
    {
        public float speed = 8f;
        Enemy target;
        float damage;

        public void Init(Enemy t, float dmg)
        {
            target = t;
            damage = dmg;
        }

        void Update()
        {
            if (!target)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, target.transform.position, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, target.transform.position) < 0.1f)
            {
                TowerDefenseUltimate.I.PlayOneShot(TowerDefenseUltimate.I.hitSound);
                Instantiate(TowerDefenseUltimate.I.hitFX, target.transform.position, Quaternion.identity);
                target.Hit(damage);
                Destroy(gameObject);
            }
        }
    }
}
