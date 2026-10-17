<script setup lang="ts">
import LocaleSwitcher from '../components/LocaleSwitcher.vue'
import { useLocalizedPageHead } from '../composables/useLocalizedPageHead'
import { useStorefrontLocale } from '../composables/useStorefrontLocale'
import { localizedPath } from '../utils/storefront-locale'

const route = useRoute()
const { locale } = useStorefrontLocale()
const copy = computed(() => ({
  'zh-TW': {
    title: '購物政策（展示版）', nav: '主要導覽', home: '返回商店', support: '客服與案件入口', version: '展示政策 v0.2 · 2026-10-13', notice: '全流程模擬；沒有實際付款、退款、配送或有效發票。這些內容供本機展示，不是正式商家條款。',
    shopping: '購物與付款', shoppingText: '商品、價格與庫存使用合成展示資料，實際可購買性由服務端回覆。結帳與付款只建立 mock 訂單，不會收取款項。',
    delivery: '配送', deliveryText: '配送方式、運費與配送進度都是示意，不會建立真實包裹或呼叫承運服務；畫面不代表到貨日期承諾。',
    returns: '退貨與退款', returnsText: '展示流程可依訂單狀態提出退貨並查看受理、收件、驗收與模擬退款結果。只有服務端授權的本人訂單可操作；模擬退款不會移動資金。此流程不設定法定退貨期限，也不代表正式退貨條款。',
    data: '資料使用', dataText: '本機展示使用合成測試資料。訂單與售後內容依登入帳號或該筆訂單的有效本人驗證能力授權；不得以訂單編號推定存取權。此頁不新增保存期限或法定分類。'
  },
  ja: {
    title: 'ショッピングポリシー（デモ版）', nav: 'メインナビゲーション', home: 'ストアに戻る', support: 'サポートと案件', version: 'デモポリシー v0.2 · 2026-10-13', notice: 'すべて模擬です。実際の支払・返金・配送・有効な請求書はありません。ローカルデモ用であり、正式な販売条件ではありません。',
    shopping: '購入と支払', shoppingText: '商品、価格、在庫は合成データです。購入可否はサービス側の応答に従います。チェックアウトと支払は模擬注文のみで、実際の請求はありません。',
    delivery: '配送', deliveryText: '配送方法、送料、配送状況はすべてデモ表示です。実際の荷物や配送業者への接続はなく、到着日の約束ではありません。',
    returns: '返品と返金', returnsText: '注文状態に応じてデモ返品を申請し、受付、受領、検品、模擬返金の状態を確認できます。サーバーが本人と認めた注文のみ操作できます。模擬返金で資金は移動しません。法定返品期間を定めるものでも、正式な返品条件でもありません。',
    data: 'データの利用', dataText: 'ローカルデモでは合成テストデータを使用します。注文とアフターサービス情報はログインアカウント、または注文ごとの有効な本人確認手段で保護されます。注文番号だけではアクセスできません。このページは保存期間や法的分類を追加しません。'
  },
  en: {
    title: 'Shopping policies (demo)', nav: 'Main navigation', home: 'Back to store', support: 'Support and cases', version: 'Demo policy v0.2 · 2026-10-13', notice: 'All flows are simulated; there are no real payments, refunds, deliveries or valid tax invoices. This is local demo content, not formal merchant terms.',
    shopping: 'Shopping and payment', shoppingText: 'Products, prices and stock use synthetic demo data. The service response determines availability. Checkout and payment create mock orders only; no funds are collected.',
    delivery: 'Delivery', deliveryText: 'Delivery methods, fees and tracking states are illustrative. No parcel is created and no carrier is contacted. The display does not promise a delivery date.',
    returns: 'Returns and refunds', returnsText: 'The demo flow can request a return and show its review, receipt, inspection and mock refund states according to the order state. Only an order authorized by the service for its owner can be used. Mock refunds move no money. This flow sets no statutory return period and is not a formal return policy.',
    data: 'Data use', dataText: 'The local demo uses synthetic test data. Order and aftercare details require the signed-in account or a valid order-specific ownership capability; an order ID alone grants no access. This page does not add a retention period or legal classification.'
  }
}[locale.value]!))

useLocalizedPageHead({ title: computed(() => copy.value.title), path: computed(() => route.path), locale, publicPage: true })
</script>

<template>
  <main class="page">
    <nav :aria-label="copy.nav" class="nav">
      <NuxtLink :to="localizedPath(locale, '/')">{{ copy.home }}</NuxtLink>
      <NuxtLink :to="localizedPath(locale, '/support')">{{ copy.support }}</NuxtLink>
      <LocaleSwitcher :locale="locale" />
    </nav>
    <h1>{{ copy.title }}</h1>
    <p class="version">{{ copy.version }}</p>
    <aside role="note" class="notice">{{ copy.notice }}</aside>
    <section aria-labelledby="shopping-heading"><h2 id="shopping-heading">{{ copy.shopping }}</h2><p>{{ copy.shoppingText }}</p></section>
    <section aria-labelledby="delivery-heading"><h2 id="delivery-heading">{{ copy.delivery }}</h2><p>{{ copy.deliveryText }}</p></section>
    <section aria-labelledby="returns-heading"><h2 id="returns-heading">{{ copy.returns }}</h2><p>{{ copy.returnsText }}</p></section>
    <section aria-labelledby="data-heading"><h2 id="data-heading">{{ copy.data }}</h2><p>{{ copy.dataText }}</p></section>
  </main>
</template>

<style scoped>
.page{max-width:52rem;margin:auto;padding:clamp(1rem,4vw,2rem);color:#25252c;font-family:system-ui,sans-serif;line-height:1.65}.nav{display:flex;align-items:center;justify-content:space-between;gap:1rem;flex-wrap:wrap}.nav>a{color:#6d2942}h1{margin:2rem 0 .25rem;font-size:clamp(1.7rem,5vw,2.4rem);line-height:1.2}.version{color:#625c58}.notice{margin:1.25rem 0;padding:1rem;border-inline-start:4px solid #8b3952;background:#fbf4f6}section{margin:1rem 0;padding:1rem;background:white;border:1px solid #ddd5d0;border-radius:.5rem}h2{margin:.1rem 0 .4rem;font-size:1.2rem}p{margin:.4rem 0}a:focus-visible{outline:3px solid #235b9d;outline-offset:3px;border-radius:.15rem}
</style>
