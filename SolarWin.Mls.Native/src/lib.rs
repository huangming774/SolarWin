use base64::{Engine as _, engine::general_purpose::STANDARD as BASE64};
use futures::executor::block_on;
use openmls_frb::api::{
    config::MlsGroupConfig,
    engine::MlsEngine,
    keys::{MlsSignatureKeyPair, serialize_signer},
    types::{MlsCiphersuite, ProcessedMessageType},
};
use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use std::{
    ffi::{CStr, CString, c_char},
    panic::{AssertUnwindSafe, catch_unwind},
    ptr,
};

const SUITE: MlsCiphersuite = MlsCiphersuite::Mls128DhkemX25519Aes128gcmSha256Ed25519;

#[derive(Serialize)]
struct BridgeResponse {
    ok: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    value: Option<Value>,
    #[serde(skip_serializing_if = "Option::is_none")]
    error: Option<String>,
}

#[derive(Deserialize)]
struct GroupRequest {
    group_id: String,
}

#[derive(Deserialize)]
struct SignerGroupRequest {
    group_id: String,
    signer: String,
}

#[derive(Deserialize)]
struct KeyPackageRequest {
    signer: String,
    public_key: String,
    identity: String,
}

#[derive(Deserialize)]
struct CreateGroupRequest {
    signer: String,
    public_key: String,
    identity: String,
    group_id: String,
}

#[derive(Deserialize)]
struct JoinWelcomeRequest {
    signer: String,
    welcome: String,
    ratchet_tree: Option<String>,
}

#[derive(Deserialize)]
struct ExternalJoinRequest {
    signer: String,
    public_key: String,
    identity: String,
    group_info: String,
    ratchet_tree: Option<String>,
}

#[derive(Deserialize)]
struct AddMembersRequest {
    group_id: String,
    signer: String,
    key_packages: Vec<String>,
}

#[derive(Deserialize)]
struct RemoveMembersRequest {
    group_id: String,
    signer: String,
    member_indices: Vec<u32>,
}

#[derive(Deserialize)]
struct CreateMessageRequest {
    group_id: String,
    signer: String,
    message: String,
    aad: Option<String>,
}

#[derive(Deserialize)]
struct ProcessMessageRequest {
    group_id: String,
    message: String,
}

fn b64(bytes: impl AsRef<[u8]>) -> String {
    BASE64.encode(bytes.as_ref())
}

fn unb64(value: &str, field: &str) -> Result<Vec<u8>, String> {
    BASE64
        .decode(value)
        .map_err(|e| format!("Invalid Base64 in {field}: {e}"))
}

fn parse<T: for<'de> Deserialize<'de>>(request: &str) -> Result<T, String> {
    serde_json::from_str(request).map_err(|e| format!("Invalid request JSON: {e}"))
}

fn operation(engine: &MlsEngine, name: &str, request: &str) -> Result<Value, String> {
    match name {
        "generate_signer" => {
            let pair = MlsSignatureKeyPair::generate(SUITE)?;
            let private = pair.private_key();
            let public = pair.public_key();
            let signer = serialize_signer(SUITE, private, public.clone())?;
            Ok(json!({ "signer": b64(signer), "public_key": b64(public) }))
        }
        "create_key_package" => {
            let req: KeyPackageRequest = parse(request)?;
            let result = block_on(engine.create_key_package(
                SUITE,
                unb64(&req.signer, "signer")?,
                unb64(&req.identity, "identity")?,
                unb64(&req.public_key, "public_key")?,
                None,
            ))?;
            Ok(json!({ "key_package": b64(result.key_package_bytes) }))
        }
        "create_group" => {
            let req: CreateGroupRequest = parse(request)?;
            let result = block_on(engine.create_group(
                MlsGroupConfig::default_config(SUITE),
                unb64(&req.signer, "signer")?,
                unb64(&req.identity, "identity")?,
                unb64(&req.public_key, "public_key")?,
                Some(unb64(&req.group_id, "group_id")?),
                None,
            ))?;
            Ok(json!({ "group_id": b64(result.group_id) }))
        }
        "join_welcome" => {
            let req: JoinWelcomeRequest = parse(request)?;
            let result = block_on(
                engine.join_group_from_welcome(
                    MlsGroupConfig::default_config(SUITE),
                    unb64(&req.welcome, "welcome")?,
                    req.ratchet_tree
                        .as_deref()
                        .map(|x| unb64(x, "ratchet_tree"))
                        .transpose()?,
                    unb64(&req.signer, "signer")?,
                ),
            )?;
            Ok(json!({ "group_id": b64(result.group_id) }))
        }
        "join_external" => {
            let req: ExternalJoinRequest = parse(request)?;
            let result = block_on(
                engine.join_group_external_commit_v2(
                    MlsGroupConfig::default_config(SUITE),
                    unb64(&req.group_info, "group_info")?,
                    req.ratchet_tree
                        .as_deref()
                        .map(|x| unb64(x, "ratchet_tree"))
                        .transpose()?,
                    unb64(&req.signer, "signer")?,
                    unb64(&req.identity, "identity")?,
                    unb64(&req.public_key, "public_key")?,
                    None,
                    false,
                    None,
                ),
            )?;
            Ok(json!({
                "group_id": b64(result.group_id),
                "commit": b64(result.commit),
                "group_info": result.group_info.map(b64),
            }))
        }
        "group_epoch" => {
            let req: GroupRequest = parse(request)?;
            let epoch = block_on(engine.group_epoch(unb64(&req.group_id, "group_id")?))?;
            Ok(json!({ "epoch": epoch }))
        }
        "group_active" => {
            let req: GroupRequest = parse(request)?;
            let active = block_on(engine.group_is_active(unb64(&req.group_id, "group_id")?))?;
            Ok(json!({ "active": active }))
        }
        "export_ratchet_tree" => {
            let req: GroupRequest = parse(request)?;
            let bytes = block_on(engine.export_ratchet_tree(unb64(&req.group_id, "group_id")?))?;
            Ok(json!({ "ratchet_tree": b64(bytes) }))
        }
        "export_group_info" => {
            let req: SignerGroupRequest = parse(request)?;
            let bytes = block_on(engine.export_group_info(
                unb64(&req.group_id, "group_id")?,
                unb64(&req.signer, "signer")?,
            ))?;
            Ok(json!({ "group_info": b64(bytes) }))
        }
        "add_members" => {
            let req: AddMembersRequest = parse(request)?;
            let packages = req
                .key_packages
                .iter()
                .map(|x| unb64(x, "key_packages"))
                .collect::<Result<Vec<_>, _>>()?;
            let result = block_on(engine.add_members(
                unb64(&req.group_id, "group_id")?,
                unb64(&req.signer, "signer")?,
                packages,
            ))?;
            Ok(json!({
                "commit": b64(result.commit),
                "welcome": b64(result.welcome),
                "group_info": result.group_info.map(b64),
            }))
        }
        "remove_members" => {
            let req: RemoveMembersRequest = parse(request)?;
            let result = block_on(engine.remove_members(
                unb64(&req.group_id, "group_id")?,
                unb64(&req.signer, "signer")?,
                req.member_indices,
            ))?;
            Ok(json!({
                "commit": b64(result.commit),
                "welcome": result.welcome.map(b64),
                "group_info": result.group_info.map(b64),
            }))
        }
        "create_message" => {
            let req: CreateMessageRequest = parse(request)?;
            let result = block_on(engine.create_message(
                unb64(&req.group_id, "group_id")?,
                unb64(&req.signer, "signer")?,
                unb64(&req.message, "message")?,
                req.aad.as_deref().map(|x| unb64(x, "aad")).transpose()?,
            ))?;
            Ok(json!({ "ciphertext": b64(result.ciphertext) }))
        }
        "process_message" => {
            let req: ProcessMessageRequest = parse(request)?;
            let result = block_on(engine.process_message(
                unb64(&req.group_id, "group_id")?,
                unb64(&req.message, "message")?,
            ))?;
            let kind = match result.message_type {
                ProcessedMessageType::Application => "application",
                ProcessedMessageType::Proposal => "proposal",
                ProcessedMessageType::StagedCommit => "commit",
            };
            Ok(json!({
                "message_type": kind,
                "sender_index": result.sender_index,
                "epoch": result.epoch,
                "application_message": result.application_message.map(b64),
                "has_staged_commit": result.has_staged_commit,
                "has_proposal": result.has_proposal,
            }))
        }
        "delete_group" => {
            let req: GroupRequest = parse(request)?;
            block_on(engine.delete_group(unb64(&req.group_id, "group_id")?))?;
            Ok(json!({}))
        }
        _ => Err(format!("Unknown MLS bridge operation: {name}")),
    }
}

fn response_json(result: Result<Value, String>) -> *mut c_char {
    let response = match result {
        Ok(value) => BridgeResponse {
            ok: true,
            value: Some(value),
            error: None,
        },
        Err(error) => BridgeResponse {
            ok: false,
            value: None,
            error: Some(error),
        },
    };
    let json = serde_json::to_string(&response)
        .unwrap_or_else(|_| "{\"ok\":false,\"error\":\"Bridge serialization failed\"}".into());
    CString::new(json)
        .expect("JSON cannot contain NUL")
        .into_raw()
}

unsafe fn string_arg<'a>(value: *const c_char, field: &str) -> Result<&'a str, String> {
    if value.is_null() {
        return Err(format!("{field} is null"));
    }
    unsafe { CStr::from_ptr(value) }
        .to_str()
        .map_err(|e| format!("{field} is not UTF-8: {e}"))
}

#[unsafe(no_mangle)]
pub extern "C" fn sw_mls_open(
    db_path: *const c_char,
    key: *const u8,
    key_len: usize,
) -> *mut MlsEngine {
    catch_unwind(AssertUnwindSafe(|| {
        let path = unsafe { string_arg(db_path, "db_path") }.ok()?;
        if key.is_null() || key_len != 32 {
            return None;
        }
        let key = unsafe { std::slice::from_raw_parts(key, key_len) }.to_vec();
        block_on(MlsEngine::create(path.to_owned(), key))
            .ok()
            .map(|engine| Box::into_raw(Box::new(engine)))
    }))
    .ok()
    .flatten()
    .unwrap_or(ptr::null_mut())
}

#[unsafe(no_mangle)]
pub extern "C" fn sw_mls_call(
    engine: *mut MlsEngine,
    name: *const c_char,
    request: *const c_char,
) -> *mut c_char {
    match catch_unwind(AssertUnwindSafe(|| {
        if engine.is_null() {
            return response_json(Err("MLS engine is null".into()));
        }
        let name = unsafe { string_arg(name, "operation") };
        let request = unsafe { string_arg(request, "request") };
        match (name, request) {
            (Ok(name), Ok(request)) => response_json(operation(unsafe { &*engine }, name, request)),
            (Err(error), _) | (_, Err(error)) => response_json(Err(error)),
        }
    })) {
        Ok(value) => value,
        Err(_) => response_json(Err("MLS bridge panicked".into())),
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn sw_mls_close(engine: *mut MlsEngine) {
    if engine.is_null() {
        return;
    }
    let _ = catch_unwind(AssertUnwindSafe(|| {
        let boxed = unsafe { Box::from_raw(engine) };
        let _ = block_on(boxed.close());
    }));
}

#[unsafe(no_mangle)]
pub extern "C" fn sw_mls_free_string(value: *mut c_char) {
    if value.is_null() {
        return;
    }
    let _ = unsafe { CString::from_raw(value) };
}
