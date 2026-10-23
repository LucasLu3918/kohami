<script setup lang="ts">
import { useLocalizedPageHead } from '../composables/useLocalizedPageHead'
import { useStorefrontLocale } from '../composables/useStorefrontLocale'
import { localizedPath } from '../utils/storefront-locale'

const route = useRoute()
const { locale } = useStorefrontLocale()
const copy = computed(() => ({
  'zh-TW': {
    title: '客服與案件入口', nav: '主要導覽', home: '返回商店', policies: '購物政策', version: '服務說明 v0.2 · 2026-10-13', notice: '真人客服與外寄通知服務尚未啟用，服務時限尚未設定。案件只記錄 UTC 時間節點與實際經過秒數；營業日曆與 SLA 尚未設定。',
    categories: '可用的案件流程', orders: '訂單、配送、退貨與退款：請先查詢自己的訂單，再依目前狀態提出或追蹤售後案件。服務端會檢查本人權限。', data: '一般問題與資料權利請求目前無法透過此頁提交。請勿在表單或公開留言輸入個人資料。', action: '開啟我的訂單與售後', auth: '找不到案件或沒有權限時，系統不會透露其他人的訂單是否存在；請確認已登入正確帳號或使用原訂單的本人驗證。', policy: '查看購物、配送、退貨、退款與資料政策。'
  },
  ja: {
    title: 'サポートと案件', nav: 'メインナビゲーション', home: 'ストアに戻る', policies: 'ショッピングポリシー', version: 'サービス案内 v0.2 · 2026-10-13', notice: '有人サポートと外部通知は未設定です。応答時間も定めていません。案件は UTC の時刻と実際の経過秒数のみ記録します。営業日カレンダーと SLA は未設定です。',
    categories: '利用できる案件フロー', orders: '注文、配送、返品、返金：まず自分の注文を確認し、注文状態に応じてアフターサービス案件を申請・追跡してください。本人確認はサービス側で行います。', data: '一般的なお問い合わせとデータ権利請求は、このページから送信できません。フォームや公開コメントに個人情報を入力しないでください。', action: '注文とアフターサービスを開く', auth: '案件が見つからない場合や権限がない場合、他人の注文の存在は表示されません。正しいアカウントでログインするか、元の注文の本人確認を使用してください。', policy: '購入、配送、返品、返金、データポリシーを確認します。'
  },
  en: {
    title: 'Customer support', nav: 'Main navigation', home: 'Back to store', policies: 'Shopping policies', version: 'Service information v0.2 · 2026-10-13', notice: 'Live support and external notifications are not enabled. Response times have not been set. Cases record UTC timestamps and elapsed seconds only; business-day calendars and SLAs are not configured.',
    categories: 'Available case flow', orders: 'Orders, delivery, returns and refunds: look up your own order first, then request or track aftercare according to its current state. The service checks ownership.', data: 'General inquiries and data-rights requests cannot be submitted from this page.', action: 'Open my order and aftercare', auth: 'If a case is unavailable or you lack access, the service does not reveal whether another person’s order exists. Sign in to the right account or use the original order-specific ownership proof.', policy: 'Read the shopping, delivery, return, refund and data policies.'
  }
}[locale.value]!))

useLocalizedPageHead({ title: computed(() => copy.value.title), path: computed(() => route.path), locale, publicPage: true })
</script>

<template>
  <main id="main-content" class="page" tabindex="-1">
    <nav :aria-label="copy.nav" class="nav">
      <NuxtLink :to="localizedPath(locale, '/')">{{ copy.home }}</NuxtLink>
      <NuxtLink :to="localizedPath(locale, '/policies')">{{ copy.policies }}</NuxtLink>
    </nav>
    <h1>{{ copy.title }}</h1>
    <p class="version">{{ copy.version }}</p>
    <aside role="note" class="notice">{{ copy.notice }}</aside>
    <section aria-labelledby="categories-heading">
      <h2 id="categories-heading">{{ copy.categories }}</h2>
      <p>{{ copy.orders }}</p>
      <p>{{ copy.data }}</p>
      <NuxtLink class="primary" :to="localizedPath(locale, '/#care-heading')">{{ copy.action }}</NuxtLink>
      <p class="muted">{{ copy.auth }}</p>
    </section>
    <p>{{ copy.policy }} <NuxtLink :to="localizedPath(locale, '/policies')">{{ copy.policies }}</NuxtLink></p>
  </main>
</template>

<style scoped>
.page{max-width:52rem;margin:auto;padding:clamp(1rem,4vw,2rem);color:var(--store-ink-strong);font-family:system-ui,sans-serif;line-height:1.65}.nav{display:flex;align-items:center;justify-content:space-between;gap:1rem;flex-wrap:wrap}.nav>a{color:var(--store-accent-strong)}h1{margin:2rem 0 .25rem;font-size:clamp(1.7rem,5vw,2.4rem);line-height:1.2}.version,.muted{color:var(--store-muted-strong)}.notice{margin:1.25rem 0;padding:1rem;border-inline-start:4px solid #8b3952;background:#fbf4f6}section{margin:1rem 0;padding:1rem;background:white;border:1px solid var(--store-border-strong);border-radius:.5rem}h2{margin:.1rem 0 .4rem;font-size:1.2rem}p{margin:.8rem 0}.primary{display:inline-block;margin:.6rem 0;padding:.7rem 1rem;background:var(--store-accent-strong);color:white;border-radius:.3rem;text-decoration:none}.primary:hover{background:var(--store-accent-hover)}a:focus-visible{outline:3px solid var(--store-focus);outline-offset:3px;border-radius:.15rem}
</style>
