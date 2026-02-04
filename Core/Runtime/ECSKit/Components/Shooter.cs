using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 射击组件 - 自动射击功能
    /// </summary>
    public struct Shooter : IComponentData
    {
        /// <summary>
        /// 射击间隔（秒）
        /// </summary>
        public float FireRate;
        
        /// <summary>
        /// 距离上次射击的时间
        /// </summary>
        public float TimeSinceLastShot;
        
        /// <summary>
        /// 每次射击的子弹数
        /// </summary>
        public int BulletsPerShot;
        
        /// <summary>
        /// 子弹速度
        /// </summary>
        public float BulletSpeed;
        
        /// <summary>
        /// 子弹生命周期
        /// </summary>
        public float BulletLifetime;
        
        /// <summary>
        /// 射击方向 (0=forward, 1=up, 2=right)
        /// </summary>
        public int ShootDirection;
        
        public Shooter(float fireRate, int bulletsPerShot = 1, float bulletSpeed = 15f, float bulletLifetime = 3f)
        {
            FireRate = fireRate;
            TimeSinceLastShot = fireRate; // 立即可以射击
            BulletsPerShot = bulletsPerShot;
            BulletSpeed = bulletSpeed;
            BulletLifetime = bulletLifetime;
            ShootDirection = 0;
        }
        
        public bool CanFire => TimeSinceLastShot >= FireRate;
    }
    
    /// <summary>
    /// 子弹标签
    /// </summary>
    public struct BulletTag : IComponentData { }
}
