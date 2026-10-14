import Foundation
import Security
import LocalAuthentication
import AppKit

// Native authority. No unattended approval switch, HTTP listener, exported private key,
// web cookie, or environment-variable credential source exists in this executable.
let tag = Data("com.kohami.m0.native.issuer.v1".utf8)
let account = "kohami-m0-native"
func stop(_ code: String) -> Never { FileHandle.standardError.write(Data((code+"\n").utf8)); exit(1) }
func stop(_ code: String, diagnostic error: Unmanaged<CFError>?) -> Never {
    guard let error else { stop(code) }
    let failure = error.takeRetainedValue()
    let domain = String(CFErrorGetDomain(failure))
    let safeDomain: String
    switch domain {
    case "NSOSStatusErrorDomain", "com.apple.security": safeDomain = "OSSTATUS"
    case "NSCocoaErrorDomain": safeDomain = "COCOA"
    default: safeDomain = "CFERROR"
    }
    stop("\(code)_\(safeDomain)_\(CFErrorGetCode(failure))")
}
func confirm(_ detail: String) -> LAContext {
    NSApplication.shared.setActivationPolicy(.accessory)
    let alert = NSAlert(); alert.messageText = "KOHAMI 本機安全操作"; alert.informativeText = detail
    alert.addButton(withTitle: "確認此精確操作"); alert.addButton(withTitle: "取消")
    NSApplication.shared.activate(ignoringOtherApps: true)
    guard alert.runModal() == .alertFirstButtonReturn else { stop("NATIVE_CANCELLED") }
    let context = LAContext(); context.touchIDAuthenticationAllowableReuseDuration = 0
    var error: NSError?
    guard context.canEvaluatePolicy(.deviceOwnerAuthentication, error: &error) else { stop("OS_AUTH_UNAVAILABLE") }
    let semaphore = DispatchSemaphore(value: 0)
    var success = false
    context.evaluatePolicy(.deviceOwnerAuthentication, localizedReason: detail) { ok, _ in success = ok; semaphore.signal() }
    semaphore.wait()
    guard success else { stop("OS_AUTH_REJECTED") }
    return context
}
func key(_ context: LAContext) -> SecKey {
    var item: CFTypeRef?
    let query: [String: Any] = [kSecClass as String:kSecClassKey,kSecAttrApplicationTag as String:tag,kSecReturnRef as String:true,kSecUseAuthenticationContext as String:context]
    guard SecItemCopyMatching(query as CFDictionary, &item) == errSecSuccess, let found = item else { stop("ISSUER_UNINITIALIZED") }
    return found as! SecKey
}
func publicDer(_ key: SecKey) -> Data {
    guard let pub = SecKeyCopyPublicKey(key), let raw = SecKeyCopyExternalRepresentation(pub, nil) as Data? else { stop("ISSUER_PUBLIC_FAILED") }
    // P-256 SubjectPublicKeyInfo prefix for uncompressed 65-byte X9.63 point.
    return Data([0x30,0x59,0x30,0x13,0x06,0x07,0x2a,0x86,0x48,0xce,0x3d,0x02,0x01,0x06,0x08,0x2a,0x86,0x48,0xce,0x3d,0x03,0x01,0x07,0x03,0x42,0x00]) + raw
}
let args=CommandLine.arguments
if args.count != 2 { stop("USAGE: broker provision|sign|credential|store-credential") }
switch args[1] {
case "provision":
    let lookupContext = LAContext(); lookupContext.interactionNotAllowed = true
    let existing: [String: Any] = [kSecClass as String:kSecClassKey,kSecAttrApplicationTag as String:tag,kSecReturnAttributes as String:true,kSecUseAuthenticationContext as String:lookupContext]
    let issuerLookupStatus = SecItemCopyMatching(existing as CFDictionary,nil)
    if issuerLookupStatus == errSecSuccess { stop("ISSUER_ALREADY_INITIALIZED") }
    guard issuerLookupStatus == errSecItemNotFound else { stop("ISSUER_LOOKUP_FAILED_OSSTATUS_\(issuerLookupStatus)") }
    let ctx = confirm("建立全新 KOHAMI M0 Secure Enclave issuer；不授予 Owner、不修改系統信任。")
    var accessError: Unmanaged<CFError>?
    guard let access = SecAccessControlCreateWithFlags(nil,kSecAttrAccessibleWhenUnlockedThisDeviceOnly,[.privateKeyUsage,.userPresence],&accessError) else { stop("ISSUER_ACL_FAILED", diagnostic: accessError) }
    let attrs: [String:Any] = [kSecAttrKeyType as String:kSecAttrKeyTypeECSECPrimeRandom,kSecAttrKeySizeInBits as String:256,kSecAttrTokenID as String:kSecAttrTokenIDSecureEnclave,kSecPrivateKeyAttrs as String:[kSecAttrIsPermanent as String:true,kSecAttrApplicationTag as String:tag,kSecAttrAccessControl as String:access],kSecUseAuthenticationContext as String:ctx]
    var error: Unmanaged<CFError>?
    guard let created=SecKeyCreateRandomKey(attrs as CFDictionary,&error) else { stop("SECURE_ENCLAVE_KEY_CREATE_FAILED", diagnostic: error) }
    FileHandle.standardOutput.write(publicDer(created))
case "sign":
    let raw=FileHandle.standardInput.readDataToEndOfFile()
    guard raw.count < 8192, let payload=String(data:raw,encoding:.utf8) else { stop("PERMIT_INVALID") }
    let fields=payload.split(separator:"\n",omittingEmptySubsequences:false)
    let actions=["InitialOwnerPrepare","InitialOwnerActivate","CredentialRecovery","RenewDemoOwnerGrant","TransferDemoOwner","RotateKeyMaterial","RestoreSecurityCheckpoint"]
    guard fields.count==11,fields[0]=="kohami-native-permit-v1",actions.contains(String(fields[2])),fields[4]=="demo_m0",let issued=Int64(fields[9]),let expires=Int64(fields[10]),expires-issued==600,abs(Int64(Date().timeIntervalSince1970)-issued)<30 else { stop("PERMIT_SCOPE_INVALID") }
    let ctx=confirm("動作: \(fields[2])\n目標: \(fields[3])\nDataset: \(fields[4])\nSQL指紋: \(fields[5])\nEpoch: \(fields[6])\n預覽hash: \(fields[7])\n理由: \(fields[8])\n效期: 10分鐘，一次使用")
    var error: Unmanaged<CFError>?
    guard let signature=SecKeyCreateSignature(key(ctx),.ecdsaSignatureMessageX962SHA256,raw as CFData,&error) as Data? else { stop("SIGNATURE_REJECTED", diagnostic: error) }
    FileHandle.standardOutput.write(signature)
case "store-credential":
    let secret=FileHandle.standardInput.readDataToEndOfFile()
    guard secret.count>=20,secret.count<512 else { stop("CREDENTIAL_INVALID") }
    _=confirm("將獨立 kohami_m0_native SQL credential 存入本機 Keychain，網站與 worker 不會取得。")
    guard let access=SecAccessControlCreateWithFlags(nil,kSecAttrAccessibleWhenUnlockedThisDeviceOnly,.userPresence,nil) else { stop("CREDENTIAL_ACL_FAILED") }
    let item: [String:Any]=[kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:account,kSecAttrAccount as String:account,kSecAttrAccessControl as String:access,kSecValueData as String:secret]
    guard SecItemAdd(item as CFDictionary,nil)==errSecSuccess else { stop("CREDENTIAL_STORE_REJECTED") }
case "credential":
    let ctx=confirm("讀取本次 KOHAMI M0 native SQL credential；只提供原生 CLI 程序記憶體。")
    let query:[String:Any]=[kSecClass as String:kSecClassGenericPassword,kSecAttrService as String:account,kSecAttrAccount as String:account,kSecReturnData as String:true,kSecUseAuthenticationContext as String:ctx]
    var result:CFTypeRef?
    guard SecItemCopyMatching(query as CFDictionary,&result)==errSecSuccess,let secret=result as? Data else { stop("CREDENTIAL_UNAVAILABLE") }
    FileHandle.standardOutput.write(secret)
default:stop("COMMAND_NOT_ALLOWED")
}
