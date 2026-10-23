<script setup lang="ts">
import { useLocalizedPageHead } from '../composables/useLocalizedPageHead'
import { useStorefrontLocale } from '../composables/useStorefrontLocale'
import { localizedPath } from '../utils/storefront-locale'

const route = useRoute()
const { locale } = useStorefrontLocale()
const copy = computed(() => ({
  'zh-TW': {
    title: '購物政策', nav: '主要導覽', home: '返回商店', support: '客服與案件入口', version: '服務說明 v0.2 · 2026-10-13', notice: '商品與訂單資料會保存於資料庫。外部收款、退款、物流與電子發票服務尚未連接；本頁不構成正式商家條款。',
    shopping: '購物與付款', shoppingText: '商品、價格、庫存與訂單會依服務端資料處理；款項收取需等待外部收款服務連接。',
    delivery: '配送', deliveryText: '配送方式、運費與配送進度會記錄於系統；承運商尚未連接，無法追蹤實際包裹或確認到貨日期。',
    returns: '退貨與退款', returnsText: '可依訂單狀態提出退貨並查看受理、收件、驗收與退款處理狀態。外部退款服務尚未連接。只有服務端授權的本人訂單可操作；外部退款服務尚未連接。此流程不設定法定退貨期限，也不代表正式退貨條款。',
    data: '資料使用', dataText: '本機商品目錄使用系統提供的商品資料。訂單與售後內容依登入帳號或該筆訂單的有效本人驗證能力授權；不得以訂單編號推定存取權。此頁不新增保存期限或法定分類。'
  },
  ja: {
    title: 'ショッピングポリシー', nav: 'メインナビゲーション', home: 'ストアに戻る', support: 'サポートと案件', version: 'サービス案内 v0.2 · 2026-10-13', notice: '注文データはシステムに保存されます。外部決済、返金、配送、電子請求書サービスは未接続です。正式な販売条件ではありません。',
    shopping: '購入と支払', shoppingText: '商品、価格、在庫、注文はサービスのデータに基づいて処理されます。外部決済サービスは未接続です。',
    delivery: '配送', deliveryText: '配送方法、送料、配送状況はシステムに記録されます。配送業者は未接続のため、荷物の追跡や到着日の確認はできません。',
    returns: '返品と返金', returnsText: '注文状態に応じて返品を申請し、受付、受領、検品、返金処理の状態を確認できます。サーバーが本人と認めた注文のみ操作できます。外部返金サービスは未接続です。法定返品期間を定めるものでも、正式な返品条件でもありません。',
    data: 'データの利用', dataText: '商品情報はシステムの商品データに基づいて表示されます。注文とアフターサービス情報はログインアカウント、または注文ごとの有効な本人確認手段で保護されます。注文番号だけではアクセスできません。このページは保存期間や法的分類を追加しません。'
  },
  en: {
    title: 'Shopping policies', nav: 'Main navigation', home: 'Back to store', support: 'Support and cases', version: 'Service information v0.2 · 2026-10-13', notice: 'Orders are stored in the database. External payment, refund, delivery and e-invoice services are not connected; this page is not formal merchant terms.',
    shopping: 'Shopping and payment', shoppingText: 'Products, prices, stock and orders are processed from service data. External payment processing is not connected.',
    delivery: 'Delivery', deliveryText: 'Delivery methods, fees and status are recorded by the system. A carrier is not connected, so parcels cannot be tracked and delivery dates are not available.',
    returns: 'Returns and refunds', returnsText: 'You can request a return and follow review, receipt, inspection and refund status according to the order state. External refunds are not connected. Only an order authorized by the service for its owner can be used. External refund processing is not connected. This flow sets no statutory return period and is not a formal return policy.',
    data: 'Data use', dataText: 'System records are stored in the database. Order and aftercare details require the signed-in account or a valid order-specific ownership capability; an order ID alone grants no access. This page does not add a retention period or legal classification.'
  }
}[locale.value]!))

useLocalizedPageHead({ title: computed(() => copy.value.title), path: computed(() => route.path), locale, publicPage: true })
</script>

<template>
  <main id="main-content" class="page" tabindex="-1">
    <nav :aria-label="copy.nav" class="nav">
      <NuxtLink :to="localizedPath(locale, '/')">{{ copy.home }}</NuxtLink>
      <NuxtLink :to="localizedPath(locale, '/support')">{{ copy.support }}</NuxtLink>
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
.page{max-width:52rem;margin:auto;padding:clamp(1rem,4vw,2rem);color:var(--store-ink-strong);font-family:system-ui,sans-serif;line-height:1.65}.nav{display:flex;align-items:center;justify-content:space-between;gap:1rem;flex-wrap:wrap}.nav>a{color:var(--store-accent-strong)}h1{margin:2rem 0 .25rem;font-size:clamp(1.7rem,5vw,2.4rem);line-height:1.2}.version{color:var(--store-muted-strong)}.notice{margin:1.25rem 0;padding:1rem;border-inline-start:4px solid #8b3952;background:#fbf4f6}section{margin:1rem 0;padding:1rem;background:white;border:1px solid var(--store-border-strong);border-radius:.5rem}h2{margin:.1rem 0 .4rem;font-size:1.2rem}p{margin:.4rem 0}a:focus-visible{outline:3px solid var(--store-focus);outline-offset:3px;border-radius:.15rem}
</style>
