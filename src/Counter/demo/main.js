let currentLang = 'zh-CN';
let currentTheme = 'cyber';
let navExpanded = true;
let audioEnabled = true;
let currentPopupContent = null; // 保存当前弹窗内容 {title, content}

// 音频播放函数
function playKeySound() {
    if (audioEnabled) {
        const audio = new Audio('demo/KeySound.mp3');
        audio.volume = 0.5;
        audio.play().catch(err => {
            console.log('Audio playback prevented:', err);
        });
    }
}

// 更新多语言文本
function updateMultilingualText() {
    const toggleNavBtn = document.getElementById('toggleNavBtn');
    const audioToggleBtn = document.getElementById('audioToggleBtn');
    const randomTipBtn = document.getElementById('randomWarningBtn');
    
    if (toggleNavBtn) {
        toggleNavBtn.title = navExpanded ? t('hideNavBar') : t('showNavBar');
    }
    
    if (audioToggleBtn) {
        audioToggleBtn.title = audioEnabled ? t('audio.off') : t('audio.on');
    }
    
    if (randomTipBtn) {
        randomTipBtn.title = t('randomTip.button');
    }
}

function init() {
    loadSettings();
    applyTheme(currentTheme);
    applyLanguage(currentLang);
    setupEventListeners();
    setupCollapsibles();
    setupFlowBoxes();
    setupNavigation();
}

function loadSettings() {
    const savedLang = localStorage.getItem('counter-doc-lang');
    const savedTheme = localStorage.getItem('counter-doc-theme');
    
    if (savedLang) {
        currentLang = savedLang;
        document.getElementById('langSelect').value = savedLang;
    }
    
    if (savedTheme) {
        currentTheme = savedTheme;
        document.getElementById('themeSelect').value = savedTheme;
    }
}

function saveSettings() {
    localStorage.setItem('counter-doc-lang', currentLang);
    localStorage.setItem('counter-doc-theme', currentTheme);
}

function applyTheme(theme) {
    currentTheme = theme;
    document.documentElement.setAttribute('data-theme', theme);
    saveSettings();
}

function applyLanguage(lang) {
    currentLang = lang;
    updateAllText();
    updateAllCodeExamples();
    updateMultilingualText();
    saveSettings();
}

function updateAllText() {
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        el.textContent = t(key);
    });
    
    document.querySelectorAll('[data-i18n-html]').forEach(el => {
        const key = el.getAttribute('data-i18n-html');
        el.innerHTML = t(key);
    });
    
    // 更新主题选择选项
    document.querySelectorAll('#themeSelect option[data-i18n]').forEach(option => {
        const key = option.getAttribute('data-i18n');
        option.textContent = t(key);
    });
}

function updateAllCodeExamples() {
    document.querySelectorAll('[data-code]').forEach(el => {
        const key = el.getAttribute('data-code');
        el.innerHTML = getCodeExample(key);
    });
}

function setupEventListeners() {
    document.getElementById('themeSelect').addEventListener('change', (e) => {
        playKeySound();
        applyTheme(e.target.value);
    });
    
    document.getElementById('langSelect').addEventListener('change', (e) => {
        playKeySound();
        applyLanguage(e.target.value);
    });
    
    // 音频开关功能
    const audioToggleBtn = document.getElementById('audioToggleBtn');
    if (audioToggleBtn) {
        audioToggleBtn.addEventListener('click', () => {
            playKeySound();
            audioEnabled = !audioEnabled;
            if (audioEnabled) {
                audioToggleBtn.textContent = '🔊';
                audioToggleBtn.title = t('audio.off');
            } else {
                audioToggleBtn.textContent = '🔇';
                audioToggleBtn.title = t('audio.on');
            }
        });
    }
}

function setupCollapsibles() {
    document.querySelectorAll('.collapsible-header').forEach(header => {
        header.addEventListener('click', () => {
            const collapsible = header.parentElement;
            collapsible.classList.toggle('open');
        });
    });
}

function setupFlowBoxes() {
    document.querySelectorAll('.flow-box[data-flow]').forEach(box => {
        box.addEventListener('click', () => {
            playKeySound();
            const flowKey = box.getAttribute('data-flow');
            const detail = document.getElementById(`flowDetail-${flowKey}`);
            
            if (detail) {
                const isActive = box.classList.contains('active');
                
                document.querySelectorAll('.flow-box').forEach(b => b.classList.remove('active'));
                document.querySelectorAll('.flow-detail').forEach(d => d.classList.remove('active'));
                
                if (!isActive) {
                    box.classList.add('active');
                    detail.classList.add('active');
                    
                    detail.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }
            }
        });
    });
}

// 计数器系统知识库
const counterKnowledgeBase = `
# 计数器系统知识库

## 核心概念
- 计数器系统：使用C#语言实现的线程安全自增计数与持久化解决方案，适用于游戏开发中实体ID分配、资源计数等场景
- 双标识符设计：
  - CounterName：恒定不变，用于持久化，跨存档保持一致
  - UID：运行时生成，用于实例查询，每次启动都不同
- long.MinValue的特殊语义：
  - 不作为有效序号
  - 表示「无历史数据」
  - 计数器初始化时使用long.MinValue + 1
- 线程安全：所有读写操作都使用锁保护
- 存档隔离：每个存档使用独立的管理器实例，不同存档指向不同文件路径

## API参考

### Counter类

#### 属性
- CounterName：计数器名称，恒定不变的唯一标识符，用于持久化
- CounterUID：运行时唯一标识符，用于实例查询，每次创建都不同
- Manager：关联的历史数据管理器实例，可为null
- IncrementStep：自增幅度，默认为1，负值取绝对值，超过最大值则设为1
- CountValue：当前计数值（下一个待使用的序号），不推荐直接读取

#### 方法
- GetAndIncrement()：返回当前值并按步长自增，推荐使用此方法获取唯一标识符
- GetAndIncrementOne()：返回当前值并自增1
- Increment()：仅自增，不返回值
- IncrementOne()：自增1，不返回值
- GetValue()：获取当前计数值，不推荐使用
- SetValue(value)：设置计数值，会破坏自增语义，可能导致ID重复，谨慎使用
- Dispose()：释放资源，从管理器和实例映射表中注销
- GetCounterByUID(uid)：静态方法，通过UID查询计数器实例

### CounterHistoryManager类

#### 属性
- RegisteredCount：已注册的计数器数量
- HistoryDataCount：历史数据条目数量

#### 方法
- Save()：保存所有已注册计数器的当前值到文件，返回bool
- Register(counter)：注册计数器，通常由Counter构造函数自动调用，返回bool
- Unregister(counterName)：注销计数器，通常由Dispose自动调用，返回bool
- HasHistoryData(counterName)：检查是否存在历史数据，返回bool
- GetHistoryValue(counterName)：获取历史值，无数据则返回long.MinValue
- Clear()：清空所有数据

## 使用流程

### 初始化
1. 创建管理器实例：指定文件路径，自动加载历史数据
2. 创建计数器实例：指定名称，可选管理器和自增步长

### 运行使用
- 调用GetAndIncrement()获取唯一ID
- 计数器会自动自增，保证线程安全

### 持久化
- 在游戏存档或退出时调用Save()
- 数据以JSON格式存储，键为计数器名称，值为当前计数值

### 资源释放
- 不再使用的计数器调用Dispose()
- 从管理器注册表和全局实例映射表中移除

## 最佳实践
- 推荐使用GetAndIncrement()而非直接读取CountValue
- 每个存档使用独立的管理器实例
- 在适当时机调用Save()保存数据
- 不再使用的计数器应调用Dispose()
- 管理器参数可为null，此时计数器不进行持久化，适用于临时计数场景
`;

function setupNavigation() {
    const navLinks = document.querySelectorAll('.nav-link');
    const sections = document.querySelectorAll('section[id]');
    const header = document.querySelector('header');
    const nav = document.querySelector('nav');
    const toggleNavBtn = document.getElementById('toggleNavBtn');
    
    // 问题输入处理
    const questionInputContainer = document.getElementById('questionInputContainer');
    const toggleQuestionInput = document.getElementById('toggleQuestionInput');
    const questionInput = document.getElementById('questionInput');
    const confirmBtn = document.getElementById('confirmBtn');
    
    // 展开/隐藏问题输入控件
    if (toggleQuestionInput && questionInputContainer) {
        toggleQuestionInput.addEventListener('click', () => {
            playKeySound();
            if (questionInputContainer.style.display === 'none') {
                // 显示控件
                questionInputContainer.style.display = 'block';
                questionInputContainer.style.opacity = '0';
                questionInputContainer.style.transform = 'scale(0.9) translateY(10px)';
                
                setTimeout(() => {
                    questionInputContainer.style.opacity = '1';
                    questionInputContainer.style.transform = 'scale(1) translateY(0)';
                }, 10);
            } else {
                // 隐藏控件
                questionInputContainer.style.opacity = '0';
                questionInputContainer.style.transform = 'scale(0.9) translateY(10px)';
                
                setTimeout(() => {
                    questionInputContainer.style.display = 'none';
                }, 300);
            }
        });
    }
    
    // 确认按钮点击事件
    if (confirmBtn) {
        confirmBtn.addEventListener('click', () => {
            playKeySound();
            const question = questionInput.value.trim();
            if (question) {
                // 将知识库和问题放入剪贴板
                const clipboardContent = `知识库：\n${counterKnowledgeBase}\n\n问题：${question}`;
                navigator.clipboard.writeText(clipboardContent).then(() => {
                    // 跳转到千问大模型界面
                    window.open('https://www.qianwen.com/chat', '_blank');
                }).catch(err => {
                    console.error('无法复制到剪贴板:', err);
                    // 即使复制失败也跳转到千问大模型
                    window.open('https://www.qianwen.com/chat', '_blank');
                });
            }
        });
    }
    
    // 动态设置导航条位置
    function updateNavPosition() {
        if (header && nav) {
            const headerHeight = header.offsetHeight;
            nav.style.top = `${headerHeight}px`;
        }
    }
    
    // 初始设置
    updateNavPosition();
    
    // 窗口大小变化时重新计算
    window.addEventListener('resize', updateNavPosition);
    
    // 导航条展开/收起功能
    if (toggleNavBtn) {
        toggleNavBtn.addEventListener('click', () => {
            playKeySound();
            navExpanded = !navExpanded;
            if (navExpanded) {
                nav.style.display = 'block';
                toggleNavBtn.title = t('hideNavBar');
                toggleNavBtn.textContent = '☰';
            } else {
                nav.style.display = 'none';
                toggleNavBtn.title = t('showNavBar');
                toggleNavBtn.textContent = '☰';
            }
        });
    }
    
    // 初始化多语言文本
    updateMultilingualText();
    
    function updateActiveNav() {
        let currentSection = '';
        
        sections.forEach(section => {
            const sectionTop = section.offsetTop - 150;
            if (window.scrollY >= sectionTop) {
                currentSection = section.getAttribute('id');
            }
        });
        
        navLinks.forEach(link => {
            link.classList.remove('active');
            if (link.getAttribute('href') === `#${currentSection}`) {
                link.classList.add('active');
            }
        });
    }
    
    window.addEventListener('scroll', updateActiveNav);
    updateActiveNav();
    
    navLinks.forEach(link => {
        link.addEventListener('click', (e) => {
            playKeySound();
            e.preventDefault();
            const targetId = link.getAttribute('href').substring(1);
            const targetSection = document.getElementById(targetId);
            
            if (targetSection) {
                const navHeight = nav.offsetHeight;
                const headerHeight = header.offsetHeight;
                const targetPosition = targetSection.offsetTop - headerHeight - navHeight - 20;
                
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });
    
    // 随机显示注意事项弹窗
    const randomWarningBtn = document.getElementById('randomWarningBtn');
    const warningPopup = document.getElementById('warningPopup');
    const closePopup = document.getElementById('closePopup');
    const popupTitle = document.getElementById('popupTitle');
    const popupContent = document.getElementById('popupContent');
    const cloudRecovery = document.getElementById('cloudRecovery');
    let popupTimeout = null;
    let cloudTimeout = null;
    let isMouseOver = false;
    
    if (randomWarningBtn && warningPopup) {
        // 初始化云朵
        if (cloudRecovery) {
            cloudRecovery.style.display = 'none';
        }
        
        // 显示弹窗的函数
        function showPopup() {
            // 清除所有相关定时器
            clearAllTimers();
            
            // 隐藏云朵
            if (cloudRecovery) {
                cloudRecovery.style.display = 'none';
            }
            
            const warningBoxes = document.querySelectorAll('.warning-box');
            if (warningBoxes.length > 0) {
                const randomIndex = Math.floor(Math.random() * warningBoxes.length);
                const randomWarning = warningBoxes[randomIndex];
                
                // 重置鼠标悬浮状态
                isMouseOver = false;
                
                // 获取注意事项标题和内容
                const title = randomWarning.querySelector('h4').textContent;
                const content = randomWarning.querySelector('p').textContent;
                
                // 保存当前弹窗内容
                currentPopupContent = { title, content };
                
                // 设置弹窗内容
                popupTitle.textContent = title;
                popupContent.textContent = content;
                
                // 完全重置样式并显示弹窗
                resetPopupStyles();
                warningPopup.style.display = 'block';
                
                // 开始计时
                startPopupTimer();
            }
        }
        
        // 开始弹窗定时器
        function startPopupTimer() {
            // 清除之前的定时器
            if (popupTimeout) {
                clearTimeout(popupTimeout);
                popupTimeout = null;
            }
            
            // 6秒后检查鼠标状态
            popupTimeout = setTimeout(() => {
                if (!isMouseOver) {
                    // 鼠标未悬浮，执行消失提示动画
                    showDisappearWarning();
                } else {
                    // 鼠标已悬浮，重置计时6秒
                    startPopupTimer();
                }
            }, 6000);
        }
        
        // 显示消失提示动画
        function showDisappearWarning() {
            // 清除之前的定时器
            if (popupTimeout) {
                clearTimeout(popupTimeout);
                popupTimeout = null;
            }
            
            // 执行提示动画
            warningPopup.style.boxShadow = '0 0 20px var(--accent-dim)';
            warningPopup.style.animation = 'pulse 1s infinite';
            
            // 2秒后再次检查
            popupTimeout = setTimeout(() => {
                if (!isMouseOver) {
                    // 鼠标仍未悬浮，执行消失动画并显示云朵
                    fadeOutPopupWithCloud();
                } else {
                    // 鼠标已悬浮，取消提示动画并重置计时
                    warningPopup.style.boxShadow = 'var(--shadow)';
                    warningPopup.style.animation = '';
                    startPopupTimer();
                }
            }, 2000);
        }
        
        // 弹窗淡出动画并显示云朵
        function fadeOutPopupWithCloud() {
            warningPopup.style.opacity = '0';
            warningPopup.style.transform = 'translateY(-20px) scale(0.9)';
            warningPopup.style.boxShadow = 'var(--shadow)';
            warningPopup.style.animation = '';
            
            // 动画结束后隐藏并显示云朵
            setTimeout(() => {
                warningPopup.style.display = 'none';
                popupTimeout = null;
                
                // 显示云朵
                if (cloudRecovery) {
                    cloudRecovery.style.display = 'flex';
                    
                    // 5秒后隐藏云朵
                    cloudTimeout = setTimeout(() => {
                        cloudRecovery.style.display = 'none';
                        cloudTimeout = null;
                    }, 5000);
                }
            }, 300);
        }
        
        // 从云朵恢复弹窗
        function showRecoveredPopup() {
            // 清除所有相关定时器
            clearAllTimers();
            
            // 隐藏云朵
            if (cloudRecovery) {
                cloudRecovery.style.display = 'none';
            }
            
            // 检查是否有保存的内容
            if (currentPopupContent) {
                // 重置鼠标悬浮状态
                isMouseOver = false;
                
                // 设置弹窗内容为保存的内容
                popupTitle.textContent = currentPopupContent.title;
                popupContent.textContent = currentPopupContent.content;
                
                // 完全重置样式并显示弹窗
                resetPopupStyles();
                warningPopup.style.display = 'block';
                
                // 开始计时
                startPopupTimer();
            } else {
                // 如果没有保存的内容，显示新的随机内容
                showPopup();
            }
        }
        
        // 清除所有定时器
        function clearAllTimers() {
            if (popupTimeout) {
                clearTimeout(popupTimeout);
                popupTimeout = null;
            }
            if (cloudTimeout) {
                clearTimeout(cloudTimeout);
                cloudTimeout = null;
            }
        }
        
        // 重置弹窗样式
        function resetPopupStyles() {
            warningPopup.style.opacity = '1';
            warningPopup.style.transform = 'translateY(0) scale(1)';
            warningPopup.style.boxShadow = 'var(--shadow)';
            warningPopup.style.animation = '';
            warningPopup.style.cursor = 'default';
        }
        
        // 从云朵恢复弹窗
        function recoverFromCloud() {
            showRecoveredPopup();
        }
        
        // 随机按钮点击事件
        randomWarningBtn.addEventListener('click', () => {
            playKeySound();
            showPopup();
        });
        
        // 鼠标悬浮事件
        warningPopup.addEventListener('mouseenter', () => {
            isMouseOver = true;
            // 取消提示动画
            warningPopup.style.boxShadow = 'var(--shadow)';
            warningPopup.style.animation = '';
        });
        
        // 鼠标离开事件
        warningPopup.addEventListener('mouseleave', () => {
            isMouseOver = false;
            // 鼠标离开后重新开始计时
            startPopupTimer();
        });
        
        // 关闭弹窗
        if (closePopup) {
            closePopup.addEventListener('click', () => {
                fadeOutPopupWithCloud();
                // 清除定时器
                if (popupTimeout) {
                    clearTimeout(popupTimeout);
                    popupTimeout = null;
                }
            });
        }
        
        // 点击弹窗外部关闭
        window.addEventListener('click', (e) => {
            if (e.target === warningPopup) {
                fadeOutPopupWithCloud();
                // 清除定时器
                if (popupTimeout) {
                    clearTimeout(popupTimeout);
                    popupTimeout = null;
                }
            }
        });
        
        // 云朵点击事件
        if (cloudRecovery) {
            cloudRecovery.addEventListener('click', () => {
                playKeySound();
                recoverFromCloud();
            });
        }
    }
}



function setupTabs(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return;
    
    const tabs = container.querySelectorAll('.tab');
    const contents = container.querySelectorAll('.tab-content');
    
    tabs.forEach(tab => {
        tab.addEventListener('click', () => {
            const target = tab.getAttribute('data-tab');
            
            tabs.forEach(t => t.classList.remove('active'));
            contents.forEach(c => c.classList.remove('active'));
            
            tab.classList.add('active');
            const targetContent = container.querySelector(`[data-content="${target}"]`);
            if (targetContent) {
                targetContent.classList.add('active');
            }
        });
    });
}

document.addEventListener('DOMContentLoaded', init);
