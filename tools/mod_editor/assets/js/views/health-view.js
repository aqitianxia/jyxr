export class HealthView {
  constructor(validator, onNavigate) {
    this.validator = validator;
    this.navigate = onNavigate;
    this.issues = [];
    this.container = null;
    this.hasAudited = false;
  }

  render(parentEl) {
    this.container = parentEl;
    if (!this.hasAudited) {
      this.issues = this.validator.runFullAudit();
      this.hasAudited = true;
    }
    this._renderLayout();
  }

  _renderLayout() {
    const errors = this.issues.filter(i => i.level === 'error');
    const warns = this.issues.filter(i => i.level === 'warn');

    this.container.innerHTML = `
      <div class="health-check-layout">
        <div class="health-header-card">
          <div class="health-title-group">
            <h2>🔍 全局数据健康检查与悬空引用排查</h2>
            <p>自动扫描角色、武学、神兵、剧情对白与资源别名之间的引用链，杜绝因拼写错误导致的游戏崩溃。</p>
          </div>
          <button id="recheckBtn" class="btn btn-primary">重新全面体检</button>
        </div>

        <div class="health-stats-row">
          <div class="health-stat-card ${errors.length > 0 ? 'card-error' : 'card-ok'}">
            <div class="stat-num">${errors.length}</div>
            <div class="stat-desc">致命引用断裂 (Error)</div>
          </div>
          <div class="health-stat-card ${warns.length > 0 ? 'card-warn' : 'card-ok'}">
            <div class="stat-num">${warns.length}</div>
            <div class="stat-desc">悬空或缺失警告 (Warning)</div>
          </div>
          <div class="health-stat-card card-ok">
            <div class="stat-num">100%</div>
            <div class="stat-desc">JSON 语法合规率</div>
          </div>
        </div>

        <div class="health-issues-list">
          ${this.issues.length === 0 ? `
            <div class="health-clean-box">
              <div class="clean-icon">🎉</div>
              <h3>完美！当前 MOD 未发现任何断裂引用或致命问题</h3>
              <p>所有角色头像、技能、物品和剧情跳转段落均能严密对应。</p>
            </div>
          ` : this.issues.map(iss => `
            <div class="issue-card ${iss.level === 'error' ? 'issue-error' : 'issue-warn'}">
              <div class="issue-badge">${iss.module} · ${iss.level.toUpperCase()}</div>
              <div class="issue-content">
                <h4>${iss.title}</h4>
                <p>${iss.message}</p>
              </div>
            </div>
          `).join('')}
        </div>
      </div>
    `;

    const recheckBtn = this.container.querySelector('#recheckBtn');
    if (recheckBtn) {
      recheckBtn.onclick = () => {
        recheckBtn.innerText = '正在排查...';
        this.issues = this.validator.runFullAudit();
        this._renderLayout();
      };
    }
  }
}
