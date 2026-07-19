文档
本条目下列内容是提供给开发者阅读的，需要有一些技术基础才能理解。
Drive API:DysonFS（Dyson Network File System）是 Solar Network drive 背后的文件服务，负责文件托管、上传处理、媒体分析以及所有文件相关操作。

通过网关访问时，将 /api 替换为服务 ID /drive。

上传 API
创建上传任务
端点： POST /api/files/upload/create

请求体：

{
  "hash": "string (文件哈希，如 SHA256)",
  "file_name": "string",
  "file_size": "long (字节)",
  "content_type": "string (如 'image/jpeg')",
  "pool_id": "string (GUID，可选)",
  "expired_at": "string (ISO 8601，可选)",
  "chunk_size": "long (字节，可选，默认 5MB)",
  "parent_id": "string (GUID，可选)",
  "overwrite_id": "string (GUID，可选)",
  "fast_mode": "bool (可选)",
  "index": "bool (可选，默认 false)"
}
如果已存在相同哈希的文件，返回已有的 CloudFile。否则返回 task_id、chunk_size 和 chunks_count。

上传分块
端点： POST /api/files/upload/chunk/{taskId}/{chunkIndex}

以 multipart/form-data 格式发送每个分块，包含 chunk 字段，内容为二进制数据。

完成上传
端点： POST /api/files/upload/complete/{taskId}

成功时返回 CloudFile 对象。

直接上传
端点： POST /api/files/upload/direct

多部分表单上传，包含元数据字段和 file 字段。支持 overwrite_id 和 fast_mode 覆盖已有文件。

快速模式
当 overwrite_id 与 fast_mode: true 同时设置时，服务器会原地覆盖现有对象（仅在目标对象被单个活动文件引用时）。若被共享，会自动回退到重新创建并交换的方式。

文件列表筛选
以下端点共享相同的列表查询解析器：

GET /api/files/me
GET /api/files/root/children
GET /api/files/:id/children
GET /api/files/unindexed
查询参数：

参数	说明
offset	分页偏移
take	每页大小
order	date、name 或 size
orderDesc	true/false
query	文件名不区分大小写的模糊匹配
name	精确文件名匹配
extension	文件扩展名（可有或无前导 .）
content_type / mime_type	精确的 MIME 类型匹配
pool_id	按存储池筛选
parent_id	按父文件夹筛选
indexed	true/false
recycled	true/false
is_folder	true/false
has_thumbnail	true/false
min_size / max_size	大小范围（字节）
created_after / created_before	日期范围（RFC3339 或 YYYY-MM-DD）
updated_after / updated_before	日期范围
文件夹
创建文件夹
端点： POST /folders

{
  "name": "项目",
  "parent_id": "..."
}
文件夹存储为 is_folder = true 且 indexed = true 的 cloud_files 行。

权限
文件通过以下方式暴露读/写/管理权限：

无权限记录 → 文件公开
private 权限记录 → 文件默认私有
权限检查从祖先文件夹继承
读取： GET /api/files/:id/permissions

更新： PUT /api/files/:id/permissions

{
  "items": [
    {
      "subject_type": "account",
      "subject_id": "...",
      "permission": "read"
    },
    {
      "subject_type": "scope",
      "subject_id": "files.manage",
      "permission": "manage"
    }
  ]
}
subject_type 可以是 public、private、account 或 scope。

文件操作
重命名
端点： PATCH /api/files/:id

{"name": "renamed-file.txt"}
批量操作
操作	端点
回收	POST /api/files/recycle/batch
恢复	POST /api/files/restore/batch
删除	POST /api/files/delete/batch
移动	POST /api/files/move/batch
移动请求体：

{
  "file_ids": ["file-id-1", "file-id-2"],
  "parent_id": "...",
  "indexed": true
}
省略 parent_id 或设为 null 将文件移回根目录
indexed 可选；设为 true/false 可更改索引状态
已索引与未索引
已索引文件 出现在 GET /api/files/root/children 和 GET /api/files/:id/children 中
未索引文件 仅出现在 GET /api/files/unindexed 中
文件夹始终已索引
可在上传时设置 index 字段，或通过批量移动端点稍后更改
配额与计费
配额值以 MB 为单位。

基础配额 = 等级配额 + 特权配额

等级	配额
Lv0	512MB
Lv10	1GB
Lv60	5GB
Lv120	10GB
特权	额外配额
特权 1	+10GB
特权 2	+25GB
特权 3	+50GB
活动 quota_records 的额外配额在基础配额之后叠加。

查询配额： GET /api/billing/quota

{
  "based_quota": 15360,
  "extra_quota": 25,
  "total_quota": 15385
}
查询用量： GET /api/billing/usage

{
  "used_quota": 300,
  "total_quota": 15385,
  "total_file_count": 2,
  "total_usage_bytes": 209715200
}
存储池计费的 cost_multiplier 影响可计量用量和配额检查。

WOPI / Collabora Online
DysonFS 支持 WOPI 主机端点，用于 Collabora Online 文档编辑。

创建编辑会话： POST /api/files/:id/edit

{
  "action_url": "https://collabora.example.com/browser/edit?WOPISrc=...",
  "action": "edit",
  "method": "POST",
  "form_fields": {
    "access_token": "TOKEN",
    "access_token_ttl": "1770000000000"
  },
  "wopi_src": "https://files.example.com/wopi/files/FILE_ID",
  "expires_at": "2026-05-29T12:00:00Z"
}
客户端应将 form_fields POST 到 action_url，通常在 iframe 中进行。

Messager API
DysonNetwork.Messager 曾经是 Sphere 服务的一部份，在蛮久前的重构之后抽离成为了一个单独的服务。 主要负责即时通讯与语音聊天等聊天页面的功能。

以下是自动生成的 API 文档，作为参考用途:
{
  "openapi": "3.0.4",
  "info": {
    "title": "DysonNetwork.Messager",
    "description": "The real-time messaging service in the Solar Network.",
    "termsOfService": "https://solsynth.dev/terms",
    "license": {
      "name": "APGLv3",
      "url": "https://www.gnu.org/licenses/agpl-3.0.html"
    },
    "version": "v1"
  },
  "paths": {
    "/messager/chat/summary": {
      "get": {
        "tags": [
          "Chat"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "object",
                  "additionalProperties": {
                    "$ref": "#/components/schemas/ChatSummaryResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "object",
                  "additionalProperties": {
                    "$ref": "#/components/schemas/ChatSummaryResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "object",
                  "additionalProperties": {
                    "$ref": "#/components/schemas/ChatSummaryResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/unread": {
      "get": {
        "tags": [
          "Chat"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              },
              "application/json": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              },
              "text/json": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/read-all": {
      "post": {
        "tags": [
          "Chat"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/subscriptions": {
      "get": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/RoomSubscriptionEntry"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/RoomSubscriptionEntry"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/RoomSubscriptionEntry"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/accounts/me/subscriptions": {
      "get": {
        "tags": [
          "Chat"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountSubscriptionEntry"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountSubscriptionEntry"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountSubscriptionEntry"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/accounts/me/status": {
      "get": {
        "tags": [
          "Chat"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ChatAccountStatusResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ChatAccountStatusResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ChatAccountStatusResponse"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/messages": {
      "get": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMessage"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMessage"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMessage"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "identity",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SendMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SendMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SendMessageRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/messages/{messageId}": {
      "get": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "messageId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "messageId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SendMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SendMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SendMessageRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "delete": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "messageId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/DeleteMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/DeleteMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/DeleteMessageRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/messages/voice": {
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "multipart/form-data": {
              "schema": {
                "required": [
                  "File"
                ],
                "type": "object",
                "properties": {
                  "File": {
                    "type": "string",
                    "format": "binary"
                  },
                  "Nonce": {
                    "maxLength": 36,
                    "type": "string"
                  },
                  "DurationMs": {
                    "type": "integer",
                    "format": "int32"
                  },
                  "RepliedMessageId": {
                    "type": "string",
                    "format": "uuid"
                  },
                  "ForwardedMessageId": {
                    "type": "string",
                    "format": "uuid"
                  }
                }
              },
              "encoding": {
                "File": {
                  "style": "form"
                },
                "Nonce": {
                  "style": "form"
                },
                "DurationMs": {
                  "style": "form"
                },
                "RepliedMessageId": {
                  "style": "form"
                },
                "ForwardedMessageId": {
                  "style": "form"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/messages/placeholder": {
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SendPlaceholderMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SendPlaceholderMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SendPlaceholderMessageRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/messages/redirect": {
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RedirectMessagesRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RedirectMessagesRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RedirectMessagesRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessage"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/voice/{voiceId}": {
      "get": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "voiceId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/messages/{messageId}/reactions": {
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "messageId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/MessageReactionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/MessageReactionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/MessageReactionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatReaction"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatReaction"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatReaction"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "messageId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "symbol",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "order",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatReaction"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatReaction"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatReaction"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/messages/{messageId}/reactions/{symbol}": {
      "delete": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "messageId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "symbol",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/sync": {
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SyncResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SyncResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SyncResponse"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/sync": {
      "post": {
        "tags": [
          "Chat"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/GlobalSyncResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/GlobalSyncResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/GlobalSyncResponse"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/autocomplete": {
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AutocompletionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AutocompletionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AutocompletionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/Autocompletion"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/Autocompletion"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/Autocompletion"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/devices/me/joined": {
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/DeviceJoinedRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/DeviceJoinedRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/DeviceJoinedRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/pins": {
      "post": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PinMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PinMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PinMessageRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessagePin"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessagePin"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMessagePin"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "includeExpired",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMessagePin"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMessagePin"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMessagePin"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/pins/{pinId}": {
      "delete": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "pinId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/bots/commands": {
      "get": {
        "tags": [
          "Chat"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "object",
                  "additionalProperties": {
                    "type": "array",
                    "items": {
                      "$ref": "#/components/schemas/SnBotCommand"
                    }
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "object",
                  "additionalProperties": {
                    "type": "array",
                    "items": {
                      "$ref": "#/components/schemas/SnBotCommand"
                    }
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "object",
                  "additionalProperties": {
                    "type": "array",
                    "items": {
                      "$ref": "#/components/schemas/SnBotCommand"
                    }
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{id}": {
      "get": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat": {
      "get": {
        "tags": [
          "ChatRoom"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/rooms/sync": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomSyncRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomSyncRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ChatRoomSyncRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ChatRoomSyncResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ChatRoomSyncResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ChatRoomSyncResponse"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/direct": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/DirectMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/DirectMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/DirectMessageRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/direct/{accountId}": {
      "get": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{id}/e2ee/enable": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/EnableE2eeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/EnableE2eeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/EnableE2eeRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{id}/mls/enable": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/EnableMlsRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/EnableMlsRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/EnableMlsRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/members/me": {
      "get": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/members/me/profile": {
      "patch": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberProfileRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberProfileRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberProfileRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/members/online": {
      "get": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/OnlineMembersResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/OnlineMembersResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/OnlineMembersResponse"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/members": {
      "get": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "withStatus",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "accountName",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMember"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMember"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMember"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/invites/{roomId}": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/invites": {
      "get": {
        "tags": [
          "ChatRoom"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMember"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMember"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatMember"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/invites/{roomId}/accept": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatRoom"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/invites/{roomId}/decline": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/members/me/notify": {
      "patch": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberNotifyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberNotifyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ChatMemberNotifyRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatMember"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/{roomId}/members/{memberId}/timeout": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatTimeoutRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ChatTimeoutRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ChatTimeoutRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "delete": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/{roomId}/members/{memberId}": {
      "delete": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/groups": {
      "get": {
        "tags": [
          "ChatRoom"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatGroup"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatGroup"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatGroup"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateGroupRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateGroupRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateGroupRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatGroup"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatGroup"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatGroup"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/groups/{groupId}": {
      "patch": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateGroupRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateGroupRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateGroupRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatGroup"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatGroup"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnChatGroup"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/rooms/{roomId}/group": {
      "patch": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/MoveToGroupRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/MoveToGroupRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/MoveToGroupRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/rooms/{roomId}/messages/{messageId}": {
      "delete": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "messageId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/DeleteChatRoomMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/DeleteChatRoomMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/DeleteChatRoomMessageRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/rooms/{roomId}/members/{accountId}/timeout": {
      "post": {
        "tags": [
          "ChatRoom"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/TimeoutUserRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/TimeoutUserRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/TimeoutUserRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/realms/{slug}/chat": {
      "get": {
        "tags": [
          "RealmChat"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatRoom"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatRoom"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnChatRoom"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/realtime/{roomId}/participants": {
      "get": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/CallParticipant"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/CallParticipant"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/CallParticipant"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/realtime/{roomId}/kick/{targetAccountId}": {
      "post": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "targetAccountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/KickParticipantRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/KickParticipantRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/KickParticipantRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/realtime/{roomId}/mute/{targetAccountId}": {
      "post": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "targetAccountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/realtime/{roomId}/unmute/{targetAccountId}": {
      "post": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "targetAccountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/realtime/{roomId}": {
      "get": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealtimeCall"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealtimeCall"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealtimeCall"
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealtimeCall"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealtimeCall"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealtimeCall"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "tool",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/messager/chat/realtime/{roomId}/join": {
      "get": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "tool",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/JoinCallResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/JoinCallResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/JoinCallResponse"
                }
              }
            }
          }
        }
      }
    },
    "/messager/chat/realtime/{roomId}/invite/{targetAccountId}": {
      "post": {
        "tags": [
          "RealtimeCall"
        ],
        "parameters": [
          {
            "name": "roomId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "targetAccountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "AccountContactType": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "AccountSubscriptionEntry": {
        "type": "object",
        "properties": {
          "room_id": {
            "type": "string",
            "format": "uuid"
          },
          "member_id": {
            "type": "string",
            "format": "uuid"
          },
          "room": {
            "$ref": "#/components/schemas/SnChatRoom"
          },
          "devices": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/DeviceSubscriptionEntry"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "Autocompletion": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "keyword": {
            "type": "string",
            "nullable": true
          },
          "data": {
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AutocompletionRequest": {
        "required": [
          "content"
        ],
        "type": "object",
        "properties": {
          "content": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "CallParticipant": {
        "type": "object",
        "properties": {
          "identity": {
            "type": "string",
            "nullable": true
          },
          "name": {
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "profile": {
            "$ref": "#/components/schemas/SnChatMember"
          },
          "joined_at": {
            "type": "string",
            "format": "date-time"
          },
          "track_sid": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ChatAccountStatusResponse": {
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "has_active_subscriptions": {
            "type": "boolean"
          },
          "has_any_web_socket_connection": {
            "type": "boolean"
          },
          "push_notifications_may_send_for_unsubscribed_rooms": {
            "type": "boolean"
          },
          "subscriptions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ChatSubscriptionRoomStatusResponse"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ChatMemberNotify": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "ChatMemberNotifyRequest": {
        "type": "object",
        "properties": {
          "notify_level": {
            "$ref": "#/components/schemas/ChatMemberNotify"
          },
          "break_until": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ChatMemberProfileRequest": {
        "type": "object",
        "properties": {
          "nick": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ChatMemberRequest": {
        "required": [
          "related_user_id",
          "role"
        ],
        "type": "object",
        "properties": {
          "related_user_id": {
            "type": "string",
            "format": "uuid"
          },
          "role": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "ChatMemberTransmissionObject": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "chat_room_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "username": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "nick": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "realm_nick": {
            "type": "string",
            "nullable": true
          },
          "realm_bio": {
            "type": "string",
            "nullable": true
          },
          "realm_experience": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "realm_level": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "realm_leveling_progress": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "realm_label": {
            "$ref": "#/components/schemas/SnRealmLabel"
          },
          "notify": {
            "$ref": "#/components/schemas/ChatMemberNotify"
          },
          "joined_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "leave_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "chat_group_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "chat_group": {
            "$ref": "#/components/schemas/SnChatGroup"
          },
          "invited_by_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "break_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "timeout_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "timeout_cause": {
            "$ref": "#/components/schemas/ChatTimeoutCause"
          }
        },
        "additionalProperties": false
      },
      "ChatRoomEncryptionMode": {
        "enum": [
          0,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "ChatRoomRequest": {
        "required": [
          "name"
        ],
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "picture_id": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "background_id": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "is_community": {
            "type": "boolean",
            "nullable": true
          },
          "is_public": {
            "type": "boolean",
            "nullable": true
          },
          "encryption_mode": {
            "$ref": "#/components/schemas/ChatRoomEncryptionMode"
          },
          "e2ee_policy": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ChatRoomSummarySyncChange": {
        "type": "object",
        "properties": {
          "room_id": {
            "type": "string",
            "format": "uuid"
          },
          "unread_count": {
            "type": "integer",
            "format": "int32"
          },
          "last_message": {
            "$ref": "#/components/schemas/SnChatMessage"
          },
          "changed_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ChatRoomSyncChange": {
        "type": "object",
        "properties": {
          "room_id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "room": {
            "$ref": "#/components/schemas/SnChatRoom"
          },
          "member": {
            "$ref": "#/components/schemas/SnChatMember"
          },
          "changed_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ChatRoomSyncRequest": {
        "required": [
          "last_sync_timestamp"
        ],
        "type": "object",
        "properties": {
          "last_sync_timestamp": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "ChatRoomSyncResponse": {
        "type": "object",
        "properties": {
          "changes": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ChatRoomSyncChange"
            },
            "nullable": true
          },
          "summaries": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ChatRoomSummarySyncChange"
            },
            "nullable": true
          },
          "groups": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnChatGroup"
            },
            "nullable": true
          },
          "current_timestamp": {
            "$ref": "#/components/schemas/Instant"
          },
          "total_count": {
            "type": "integer",
            "format": "int32"
          },
          "summary_total_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "ChatRoomType": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "ChatSubscriptionDeviceStatusResponse": {
        "type": "object",
        "properties": {
          "device_token": {
            "type": "string",
            "nullable": true
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_web_socket_connected": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "ChatSubscriptionRoomStatusResponse": {
        "type": "object",
        "properties": {
          "room_id": {
            "type": "string",
            "format": "uuid"
          },
          "member_id": {
            "type": "string",
            "format": "uuid"
          },
          "room": {
            "$ref": "#/components/schemas/SnChatRoom"
          },
          "is_subscribed": {
            "type": "boolean"
          },
          "push_notifications_suppressed": {
            "type": "boolean"
          },
          "devices": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ChatSubscriptionDeviceStatusResponse"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ChatSummaryResponse": {
        "type": "object",
        "properties": {
          "unread_count": {
            "type": "integer",
            "format": "int32"
          },
          "last_message": {
            "$ref": "#/components/schemas/SnChatMessage"
          }
        },
        "additionalProperties": false
      },
      "ChatTimeoutCause": {
        "type": "object",
        "properties": {
          "reason": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/ChatTimeoutCauseType"
          },
          "sender_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "since": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ChatTimeoutCauseType": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "ChatTimeoutRequest": {
        "type": "object",
        "properties": {
          "reason": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "timeout_until": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ContentSensitiveMark": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6,
          7,
          8,
          9,
          10,
          11,
          12
        ],
        "type": "integer",
        "format": "int32"
      },
      "CreateGroupRequest": {
        "required": [
          "name"
        ],
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "minLength": 1,
            "type": "string"
          },
          "color": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "DeleteChatRoomMessageRequest": {
        "type": "object",
        "properties": {
          "reason": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "DeleteMessageRequest": {
        "type": "object",
        "properties": {
          "client_message_id": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "ciphertext": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_header": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_signature": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_scheme": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "encryption_epoch": {
            "type": "integer",
            "format": "int64",
            "nullable": true
          },
          "encryption_message_type": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "DeviceJoinedRequest": {
        "required": [
          "mls_device_id"
        ],
        "type": "object",
        "properties": {
          "mls_device_id": {
            "minLength": 1,
            "type": "string"
          },
          "epoch": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "DeviceSubscriptionEntry": {
        "type": "object",
        "properties": {
          "device_token": {
            "type": "string",
            "nullable": true
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "DirectMessageRequest": {
        "required": [
          "related_user_id"
        ],
        "type": "object",
        "properties": {
          "related_user_id": {
            "type": "string",
            "format": "uuid"
          },
          "encryption_mode": {
            "$ref": "#/components/schemas/ChatRoomEncryptionMode"
          }
        },
        "additionalProperties": false
      },
      "EnableE2eeRequest": {
        "type": "object",
        "properties": {
          "encryption_mode": {
            "$ref": "#/components/schemas/ChatRoomEncryptionMode"
          }
        },
        "additionalProperties": false
      },
      "EnableMlsRequest": {
        "type": "object",
        "properties": {
          "mls_group_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "e2ee_policy": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "GlobalSyncResponse": {
        "type": "object",
        "properties": {
          "messages": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnChatMessage"
            },
            "nullable": true
          },
          "current_timestamp": {
            "$ref": "#/components/schemas/Instant"
          },
          "current_message_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "total_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "Instant": {
        "type": "object",
        "additionalProperties": false
      },
      "JoinCallResponse": {
        "type": "object",
        "properties": {
          "provider": {
            "type": "string",
            "nullable": true
          },
          "endpoint": {
            "type": "string",
            "nullable": true
          },
          "token": {
            "type": "string",
            "nullable": true
          },
          "call_id": {
            "type": "string",
            "format": "uuid"
          },
          "room_name": {
            "type": "string",
            "nullable": true
          },
          "room_title": {
            "type": "string",
            "nullable": true
          },
          "is_admin": {
            "type": "boolean"
          },
          "participants": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/CallParticipant"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "KickParticipantRequest": {
        "type": "object",
        "properties": {
          "ban_duration_minutes": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "reason": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "MessageReactionAttitude": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "MessageReactionRequest": {
        "type": "object",
        "properties": {
          "symbol": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "attitude": {
            "$ref": "#/components/schemas/MessageReactionAttitude"
          }
        },
        "additionalProperties": false
      },
      "MoveToGroupRequest": {
        "type": "object",
        "properties": {
          "group_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "OnlineMembersResponse": {
        "type": "object",
        "properties": {
          "online_count": {
            "type": "integer",
            "format": "int32"
          },
          "direct_message_status": {
            "$ref": "#/components/schemas/SnAccountStatus"
          },
          "online_user_names": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "online_accounts": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccount"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PinMessageRequest": {
        "required": [
          "message_id"
        ],
        "type": "object",
        "properties": {
          "message_id": {
            "type": "string",
            "format": "uuid"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "RedirectMessagesRequest": {
        "required": [
          "message_ids"
        ],
        "type": "object",
        "properties": {
          "message_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            }
          }
        },
        "additionalProperties": false
      },
      "RoomSubscriptionEntry": {
        "type": "object",
        "properties": {
          "room_id": {
            "type": "string",
            "format": "uuid"
          },
          "member_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "member": {
            "$ref": "#/components/schemas/SnChatMember"
          },
          "devices": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/DeviceSubscriptionEntry"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SendMessageRequest": {
        "type": "object",
        "properties": {
          "content": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "nonce": {
            "maxLength": 36,
            "type": "string",
            "nullable": true
          },
          "client_message_id": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "fund_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "survey_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "meet_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "notable_day_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "calendar_event_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "location_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "location_address": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location_wkt": {
            "type": "string",
            "nullable": true
          },
          "attachments_id": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "embeds": {
            "type": "array",
            "items": {
              "type": "object",
              "additionalProperties": { }
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "replied_message_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "forwarded_message_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "is_encrypted": {
            "type": "boolean"
          },
          "ciphertext": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_header": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_signature": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_scheme": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "encryption_epoch": {
            "type": "integer",
            "format": "int64",
            "nullable": true
          },
          "encryption_message_type": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SendPlaceholderMessageRequest": {
        "required": [
          "kind"
        ],
        "type": "object",
        "properties": {
          "kind": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "SnAccount": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "nick": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "language": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "region": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_superuser": {
            "type": "boolean"
          },
          "automated_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "profile": {
            "$ref": "#/components/schemas/SnAccountProfile"
          },
          "contacts": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountContact"
            },
            "nullable": true
          },
          "badges": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountBadge"
            },
            "nullable": true
          },
          "perk_subscription": {
            "$ref": "#/components/schemas/SnSubscriptionReferenceObject"
          },
          "perk_level": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SnAccountBadge": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "caption": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountBadgeRef": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "label": {
            "type": "string",
            "nullable": true
          },
          "caption": {
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountBoardItem": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "kind": {
            "$ref": "#/components/schemas/SnAccountBoardItemKind"
          },
          "widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "custom_app_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "custom_app_widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "is_enabled": {
            "type": "boolean"
          },
          "payload": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountBoardItemKind": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "SnAccountContact": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/AccountContactType"
          },
          "verified_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_primary": {
            "type": "boolean"
          },
          "is_public": {
            "type": "boolean"
          },
          "content": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountProfile": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "first_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "middle_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "last_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "gender": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "pronouns": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "time_zone": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "links": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnProfileLink"
            },
            "nullable": true
          },
          "username_color": {
            "$ref": "#/components/schemas/UsernameColor"
          },
          "birthday": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_seen_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "verification": {
            "$ref": "#/components/schemas/SnVerificationMark"
          },
          "active_badge": {
            "$ref": "#/components/schemas/SnAccountBadgeRef"
          },
          "experience": {
            "type": "integer",
            "format": "int32"
          },
          "level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "leveling_progress": {
            "type": "number",
            "format": "double",
            "readOnly": true
          },
          "social_credits": {
            "type": "number",
            "format": "double"
          },
          "social_credits_level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "picture": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "board": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountBoardItem"
            },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountStatus": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "attitude": {
            "$ref": "#/components/schemas/StatusAttitude"
          },
          "is_online": {
            "type": "boolean"
          },
          "is_idle": {
            "type": "boolean"
          },
          "idle_since": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_customized": {
            "type": "boolean"
          },
          "type": {
            "$ref": "#/components/schemas/StatusType"
          },
          "label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "symbol": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "cleared_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "app_identifier": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "is_automated": {
            "type": "boolean"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnBotCommand": {
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "usage": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "parameters": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnBotCommandParameter"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnBotCommandParameter": {
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "required": {
            "type": "boolean"
          },
          "type": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnChatGroup": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "color": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "room_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnChatMember": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "chat_room_id": {
            "type": "string",
            "format": "uuid"
          },
          "chat_room": {
            "$ref": "#/components/schemas/SnChatRoom"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "username": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "status": {
            "$ref": "#/components/schemas/SnAccountStatus"
          },
          "nick": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "realm_nick": {
            "type": "string",
            "nullable": true
          },
          "realm_bio": {
            "type": "string",
            "nullable": true
          },
          "realm_experience": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "realm_level": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "realm_leveling_progress": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "realm_label": {
            "$ref": "#/components/schemas/SnRealmLabel"
          },
          "chat_group_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "chat_group": {
            "$ref": "#/components/schemas/SnChatGroup"
          },
          "notify": {
            "$ref": "#/components/schemas/ChatMemberNotify"
          },
          "last_read_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "joined_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "leave_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "invited_by_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "invited_by": {
            "$ref": "#/components/schemas/SnChatMember"
          },
          "break_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "timeout_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "timeout_cause": {
            "$ref": "#/components/schemas/ChatTimeoutCause"
          }
        },
        "additionalProperties": false
      },
      "SnChatMessage": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "room_sequence": {
            "type": "integer",
            "format": "int64"
          },
          "type": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "content": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "is_encrypted": {
            "type": "boolean"
          },
          "ciphertext": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_header": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_signature": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "encryption_scheme": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "encryption_epoch": {
            "type": "integer",
            "format": "int64",
            "nullable": true
          },
          "encryption_message_type": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "client_message_id": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "members_mentioned": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "nonce": {
            "maxLength": 36,
            "type": "string",
            "nullable": true
          },
          "edited_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "attachments": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnCloudFileReferenceObject"
            },
            "nullable": true
          },
          "reactions_count": {
            "type": "object",
            "additionalProperties": {
              "type": "integer",
              "format": "int32"
            },
            "nullable": true
          },
          "reactions_made": {
            "type": "object",
            "additionalProperties": {
              "type": "boolean"
            },
            "nullable": true
          },
          "reactions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnChatReaction"
            },
            "nullable": true
          },
          "replied_message_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "replied_message": {
            "$ref": "#/components/schemas/SnChatMessage"
          },
          "forwarded_message_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "forwarded_message": {
            "$ref": "#/components/schemas/SnChatMessage"
          },
          "sender_id": {
            "type": "string",
            "format": "uuid"
          },
          "sender": {
            "$ref": "#/components/schemas/SnChatMember"
          },
          "chat_room_id": {
            "type": "string",
            "format": "uuid"
          },
          "chat_room": {
            "$ref": "#/components/schemas/SnChatRoom"
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnChatMessagePin": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "message_id": {
            "type": "string",
            "format": "uuid"
          },
          "chat_room_id": {
            "type": "string",
            "format": "uuid"
          },
          "pinned_by_member_id": {
            "type": "string",
            "format": "uuid"
          },
          "pinned_by": {
            "$ref": "#/components/schemas/SnChatMember"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnChatReaction": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "message_id": {
            "type": "string",
            "format": "uuid"
          },
          "sender_id": {
            "type": "string",
            "format": "uuid"
          },
          "sender": {
            "$ref": "#/components/schemas/SnChatMember"
          },
          "symbol": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "attitude": {
            "$ref": "#/components/schemas/MessageReactionAttitude"
          }
        },
        "additionalProperties": false
      },
      "SnChatRoom": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/ChatRoomType"
          },
          "is_community": {
            "type": "boolean"
          },
          "is_public": {
            "type": "boolean"
          },
          "encryption_mode": {
            "$ref": "#/components/schemas/ChatRoomEncryptionMode"
          },
          "mls_group_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "e2ee_policy": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "picture": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "realm": {
            "$ref": "#/components/schemas/SnRealm"
          },
          "members": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ChatMemberTransmissionObject"
            },
            "nullable": true
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnCloudFileReferenceObject": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "nullable": true
          },
          "name": {
            "type": "string",
            "nullable": true
          },
          "file_meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "user_meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "sensitive_marks": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ContentSensitiveMark"
            },
            "nullable": true
          },
          "mime_type": {
            "type": "string",
            "nullable": true
          },
          "hash": {
            "type": "string",
            "nullable": true
          },
          "size": {
            "type": "integer",
            "format": "int64"
          },
          "has_compression": {
            "type": "boolean"
          },
          "url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "width": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "height": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "blurhash": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "usage": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "application_type": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnProfileLink": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "url": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnRealm": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "slug": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "is_community": {
            "type": "boolean"
          },
          "is_public": {
            "type": "boolean"
          },
          "picture": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "verification": {
            "$ref": "#/components/schemas/SnVerificationMark"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "boost_points": {
            "type": "number",
            "format": "double"
          },
          "boost_level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnRealmLabel": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "color": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "created_by_account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnRealtimeCall": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "sender_id": {
            "type": "string",
            "format": "uuid"
          },
          "sender": {
            "$ref": "#/components/schemas/SnChatMember"
          },
          "room_id": {
            "type": "string",
            "format": "uuid"
          },
          "room": {
            "$ref": "#/components/schemas/SnChatRoom"
          },
          "provider_name": {
            "type": "string",
            "nullable": true
          },
          "session_id": {
            "type": "string",
            "nullable": true
          },
          "upstream_config_json": {
            "type": "string",
            "nullable": true
          },
          "upstream_config": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnSubscriptionReferenceObject": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "identifier": {
            "type": "string",
            "nullable": true
          },
          "group_identifier": {
            "type": "string",
            "nullable": true
          },
          "display_name": {
            "type": "string",
            "nullable": true
          },
          "perk_level": {
            "type": "integer",
            "format": "int32"
          },
          "is_testing": {
            "type": "boolean"
          },
          "begun_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_active": {
            "type": "boolean"
          },
          "is_available": {
            "type": "boolean"
          },
          "is_pending_activation": {
            "type": "boolean"
          },
          "is_free_trial": {
            "type": "boolean"
          },
          "status": {
            "$ref": "#/components/schemas/SubscriptionStatus"
          },
          "base_price": {
            "type": "number",
            "format": "double"
          },
          "final_price": {
            "type": "number",
            "format": "double"
          },
          "renewal_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnVerificationMark": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/VerificationMarkType"
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "verified_by": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "StatusAttitude": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "StatusType": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "SubscriptionStatus": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "SyncRequest": {
        "required": [
          "last_sync_timestamp"
        ],
        "type": "object",
        "properties": {
          "last_sync_timestamp": {
            "type": "integer",
            "format": "int64"
          },
          "last_sync_message_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "missing_sequences": {
            "type": "array",
            "items": {
              "type": "integer",
              "format": "int64"
            },
            "nullable": true
          },
          "missing_sequence_ranges": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SyncSequenceRangeRequest"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SyncResponse": {
        "type": "object",
        "properties": {
          "messages": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnChatMessage"
            },
            "nullable": true
          },
          "current_timestamp": {
            "$ref": "#/components/schemas/Instant"
          },
          "total_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SyncSequenceRangeRequest": {
        "type": "object",
        "properties": {
          "start_sequence": {
            "type": "integer",
            "format": "int64"
          },
          "end_sequence": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "TimeoutUserRequest": {
        "required": [
          "duration_minutes"
        ],
        "type": "object",
        "properties": {
          "duration_minutes": {
            "type": "integer",
            "format": "int32"
          },
          "reason": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateGroupRequest": {
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "color": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UsernameColor": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "value": {
            "type": "string",
            "nullable": true
          },
          "direction": {
            "type": "string",
            "nullable": true
          },
          "colors": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "VerificationMarkType": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6
        ],
        "type": "integer",
        "format": "int32"
      }
    },
    "securitySchemes": {
      "Bearer": {
        "type": "http",
        "description": "Solar Network Unified Authentication",
        "scheme": "Bearer",
        "bearerFormat": "JWT"
      }
    }
  },
  "tags": [
    {
      "name": "Chat"
    },
    {
      "name": "ChatRoom"
    },
    {
      "name": "RealmChat"
    },
    {
      "name": "RealtimeCall"
    }
  ]
}

Padlock API
DysonNetwork.Padlock 主要负责用户登陆，审计等一系列安全的服务。
以下是自动生成的 API 文档，作为参考用途:{
  "openapi": "3.0.4",
  "info": {
    "title": "DysonNetwork.Padlock",
    "description": "The authentication and authorization service in the Solar Network.",
    "termsOfService": "https://solsynth.dev/terms",
    "license": {
      "name": "APGLv3",
      "url": "https://www.gnu.org/licenses/agpl-3.0.html"
    },
    "version": "v1"
  },
  "paths": {
    "/padlock/accounts/validate": {
      "post": {
        "tags": [
          "Account"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AccountCreateValidateRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AccountCreateValidateRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AccountCreateValidateRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "string"
                }
              },
              "application/json": {
                "schema": {
                  "type": "string"
                }
              },
              "text/json": {
                "schema": {
                  "type": "string"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/accounts": {
      "post": {
        "tags": [
          "Account"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AccountCreateRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AccountCreateRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AccountCreateRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/actions": {
      "get": {
        "tags": [
          "AccountActionLog"
        ],
        "parameters": [
          {
            "name": "action",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnActionLog"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnActionLog"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnActionLog"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "orderBy",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AdminAccountSummaryResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AdminAccountSummaryResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AdminAccountSummaryResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountDetailResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountDetailResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountDetailResponse"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/devices": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "includeDeleted",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "includeSessions",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": true
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthClientWithSessions"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthClientWithSessions"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthClientWithSessions"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/devices/{deviceId}/label": {
      "patch": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "deviceId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAdminDeviceLabelRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAdminDeviceLabelRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAdminDeviceLabelRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/devices/{deviceId}/sessions/revoke": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "deviceId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/devices/{deviceId}": {
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "deviceId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/sessions": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/SessionType"
            }
          },
          {
            "name": "clientId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "includeChildren",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "activeOnly",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/sessions/{sessionId}/children": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "sessionId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/sessions/{sessionId}": {
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "sessionId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/contacts": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminAccountContactRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminAccountContactRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminAccountContactRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/contacts/{contactId}": {
      "patch": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "contactId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAdminAccountContactRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAdminAccountContactRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAdminAccountContactRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "contactId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/contacts/{contactId}/verify/request": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "contactId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/contacts/{contactId}/verify": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "contactId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminContactVerificationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminContactVerificationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminContactVerificationRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "contactId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/contacts/{contactId}/primary": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "contactId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/contacts/{contactId}/visibility": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "contactId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SetAdminContactVisibilityRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetAdminContactVisibilityRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetAdminContactVisibilityRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/factors": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountAuthFactorSummary"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountAuthFactorSummary"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountAuthFactorSummary"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminAccountAuthFactorRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminAccountAuthFactorRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminAccountAuthFactorRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/factors/{factorId}/enable": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "factorId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "string"
              }
            },
            "text/json": {
              "schema": {
                "type": "string"
              }
            },
            "application/*+json": {
              "schema": {
                "type": "string"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/factors/{factorId}/disable": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "factorId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/factors/password/reset": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminResetPasswordFactorRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminResetPasswordFactorRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminResetPasswordFactorRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountAuthFactorSummary"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/factors/{factorId}": {
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "factorId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/notifications": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SendAdminNotificationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SendAdminNotificationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SendAdminNotificationRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AdminMessageDispatchResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminMessageDispatchResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminMessageDispatchResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/emails": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SendAdminEmailRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SendAdminEmailRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SendAdminEmailRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AdminMessageDispatchResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminMessageDispatchResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminMessageDispatchResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/emails/export": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "AccountId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "AccountIds",
            "in": "query",
            "schema": {
              "type": "array",
              "items": {
                "type": "string",
                "format": "uuid"
              }
            }
          },
          {
            "name": "BroadcastToAll",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/punishments/created": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPunishment"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPunishment"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPunishment"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/punishments": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePunishmentRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePunishmentRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePunishmentRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/punishments/{punishmentId}": {
      "patch": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "punishmentId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePunishmentRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePunishmentRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePunishmentRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "punishmentId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/suspend": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SuspendAccountRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SuspendAccountRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SuspendAccountRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/sessions/revoke": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/accounts/{name}/activate": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/accounts/me": {
      "patch": {
        "tags": [
          "AccountCurrent"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BasicInfoRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BasicInfoRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BasicInfoRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/accounts/me/pin-status": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PinStatusResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PinStatusResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PinStatusResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/stats/users/geography": {
      "get": {
        "tags": [
          "AccountGeographyStatsAdmin"
        ],
        "parameters": [
          {
            "name": "since",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "precision",
            "in": "query",
            "schema": {
              "type": "string",
              "default": "country"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AccountGeographyStatsResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountGeographyStatsResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountGeographyStatsResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/accounts/{name}/punishments": {
      "get": {
        "tags": [
          "AccountPunishment"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPunishment"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPunishment"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPunishment"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/accounts/{name}/punishments/overview": {
      "get": {
        "tags": [
          "AccountPunishment"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPunishment"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/accounts/me/punishments": {
      "get": {
        "tags": [
          "AccountPunishment"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AccountPunishmentResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountPunishmentResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountPunishmentResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/identity": {
      "get": {
        "tags": [
          "AccountSecurity"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/factors": {
      "get": {
        "tags": [
          "AccountSecurity"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountAuthFactor"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountAuthFactor"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountAuthFactor"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AuthFactorRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AuthFactorRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AuthFactorRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/factors/passkey/start": {
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyRegistrationStartRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyRegistrationStartRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyRegistrationStartRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyRegistrationStartResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyRegistrationStartResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyRegistrationStartResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/factors/passkey/complete": {
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyRegistrationCompleteRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyRegistrationCompleteRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyRegistrationCompleteRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPasskey"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPasskey"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPasskey"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/factors/passkey": {
      "get": {
        "tags": [
          "AccountSecurity"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPasskey"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPasskey"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountPasskey"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/factors/passkey/{id}": {
      "delete": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "patch": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePasskeyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePasskeyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePasskeyRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPasskey"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPasskey"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountPasskey"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/factors/{id}/enable": {
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "string"
              }
            },
            "text/json": {
              "schema": {
                "type": "string"
              }
            },
            "application/*+json": {
              "schema": {
                "type": "string"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/factors/{id}/disable": {
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountAuthFactor"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/factors/{id}": {
      "delete": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/devices": {
      "get": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthClientWithSessions"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthClientWithSessions"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthClientWithSessions"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/sessions": {
      "get": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/SessionType"
            }
          },
          {
            "name": "clientId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "includeChildren",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/sessions/{id}/children": {
      "get": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthSession"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/sessions/{id}": {
      "delete": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/devices/{deviceId}": {
      "delete": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "deviceId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/sessions/current": {
      "delete": {
        "tags": [
          "AccountSecurity"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/authorized-apps": {
      "get": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/AuthorizedAppType"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AuthorizedAppResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AuthorizedAppResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AuthorizedAppResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/authorized-apps/{id}/scopes": {
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AuthorizeAppScopesRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AuthorizeAppScopesRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AuthorizeAppScopesRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AuthorizedAppResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AuthorizedAppResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AuthorizedAppResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/authorized-apps/{id}": {
      "delete": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/AuthorizedAppType"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/devices/{deviceId}/label": {
      "patch": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "deviceId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "string"
              }
            },
            "text/json": {
              "schema": {
                "type": "string"
              }
            },
            "application/*+json": {
              "schema": {
                "type": "string"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/devices/current/label": {
      "patch": {
        "tags": [
          "AccountSecurity"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "string"
              }
            },
            "text/json": {
              "schema": {
                "type": "string"
              }
            },
            "application/*+json": {
              "schema": {
                "type": "string"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/contacts": {
      "get": {
        "tags": [
          "AccountSecurity"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AccountContactRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AccountContactRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AccountContactRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/contacts/{id}/verify": {
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/contacts/{id}/primary": {
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/contacts/{id}/public": {
      "post": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountContact"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/contacts/{id}": {
      "delete": {
        "tags": [
          "AccountSecurity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/api-keys": {
      "get": {
        "tags": [
          "ApiKey"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "ApiKey"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateApiKeyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateApiKeyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateApiKeyRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/api-keys/{id}": {
      "delete": {
        "tags": [
          "ApiKey"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/api-keys/{id}/rotate": {
      "post": {
        "tags": [
          "ApiKey"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/challenge": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ChallengeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ChallengeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ChallengeRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/challenge/{id}": {
      "get": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PerformChallengeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PerformChallengeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PerformChallengeRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/challenge/{id}/factors": {
      "get": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountAuthFactor"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountAuthFactor"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountAuthFactor"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/challenge/{id}/factors/{factorId}": {
      "post": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "factorId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/challenge/{id}/passkey/start": {
      "post": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyAuthenticationStartResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyAuthenticationStartResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyAuthenticationStartResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/challenge/{id}/passkey/complete": {
      "post": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyAuthenticationCompleteRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyAuthenticationCompleteRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyAuthenticationCompleteRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/passkey/start": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyLoginStartRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyLoginStartRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyLoginStartRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyLoginStartResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyLoginStartResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PasskeyLoginStartResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/passkey/{id}/complete": {
      "post": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyAuthenticationCompleteRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyAuthenticationCompleteRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PasskeyAuthenticationCompleteRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAuthChallenge"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/challenge/pending": {
      "get": {
        "tags": [
          "Auth"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthChallenge"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthChallenge"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAuthChallenge"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/challenge/{id}/approve": {
      "post": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/challenge/{id}/decline": {
      "post": {
        "tags": [
          "Auth"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/token": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/TokenExchangeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/TokenExchangeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/TokenExchangeRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/refresh": {
      "post": {
        "tags": [
          "Auth"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/captcha": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "string"
              }
            },
            "text/json": {
              "schema": {
                "type": "string"
              }
            },
            "application/*+json": {
              "schema": {
                "type": "string"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "get": {
        "tags": [
          "Captcha"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/recover": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RecoveryRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RecoveryRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RecoveryRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/logout": {
      "post": {
        "tags": [
          "Auth"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/login/session": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/NewSessionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/NewSessionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/NewSessionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/me": {
      "get": {
        "tags": [
          "Auth"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/sudo": {
      "post": {
        "tags": [
          "Auth"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SudoRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/cache/stats": {
      "get": {
        "tags": [
          "CacheAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/CacheStatsSnapshot"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheStatsSnapshot"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheStatsSnapshot"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/cache/groups/{group}": {
      "get": {
        "tags": [
          "CacheAdmin"
        ],
        "parameters": [
          {
            "name": "group",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/CacheGroupResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheGroupResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheGroupResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/cache/keys/clear": {
      "post": {
        "tags": [
          "CacheAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ClearKeyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ClearKeyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ClearKeyRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/cache/groups/clear": {
      "post": {
        "tags": [
          "CacheAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ClearGroupRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ClearGroupRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ClearGroupRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/cache/clear": {
      "post": {
        "tags": [
          "CacheAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/CacheClearResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/captcha/verify": {
      "post": {
        "tags": [
          "Captcha"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CaptchaVerifyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CaptchaVerifyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CaptchaVerifyRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/connections": {
      "get": {
        "tags": [
          "Connection"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountConnection"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountConnection"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountConnection"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/connections/{id}": {
      "delete": {
        "tags": [
          "Connection"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/connections/{id}/visibility": {
      "post": {
        "tags": [
          "Connection"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SetConnectionVisibilityRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetConnectionVisibilityRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetConnectionVisibilityRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountConnection"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountConnection"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountConnection"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/connect/apple/mobile": {
      "post": {
        "tags": [
          "Connection"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AppleMobileConnectRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AppleMobileConnectRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AppleMobileConnectRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/callback/{provider}": {
      "get": {
        "tags": [
          "Connection"
        ],
        "parameters": [
          {
            "name": "provider",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "Connection"
        ],
        "parameters": [
          {
            "name": "provider",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/private/apps/{appId}/accounts/{accountId}/contacts": {
      "get": {
        "tags": [
          "CustomAppContact"
        ],
        "parameters": [
          {
            "name": "appId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/AccountContactType"
            }
          },
          {
            "name": "verifiedOnly",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/private/apps/{appId}/notifications": {
      "post": {
        "tags": [
          "CustomAppNotification"
        ],
        "parameters": [
          {
            "name": "appId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SendCustomAppNotificationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SendCustomAppNotificationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SendCustomAppNotificationRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/.well-known/apple-app-site-association": {
      "get": {
        "tags": [
          "DysonNetwork.Padlock"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/.well-known/assetlinks.json": {
      "get": {
        "tags": [
          "DysonNetwork.Padlock"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/e2ee/mls/devices/me/kps": {
      "put": {
        "tags": [
          "E2Ee"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PublishMlsKeyPackageBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PublishMlsKeyPackageBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PublishMlsKeyPackageBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsKeyPackage"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsKeyPackage"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsKeyPackage"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/kp/status": {
      "get": {
        "tags": [
          "E2Ee"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/MlsKeyPackageStatusResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/MlsKeyPackageStatusResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/MlsKeyPackageStatusResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/keys/{accountId}/devices": {
      "get": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "consume",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": true
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/MlsDeviceKeyPackageResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/MlsDeviceKeyPackageResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/MlsDeviceKeyPackageResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/users/ready/batch": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchCheckMlsReadyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchCheckMlsReadyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BatchCheckMlsReadyRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/BatchCheckMlsReadyResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/BatchCheckMlsReadyResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/BatchCheckMlsReadyResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/users/{accountId}/ready": {
      "get": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/CheckMlsReadyResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/CheckMlsReadyResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/CheckMlsReadyResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/devices/capable": {
      "get": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/MlsDeviceKeyPackageResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/MlsDeviceKeyPackageResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/MlsDeviceKeyPackageResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/bootstrap": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BootstrapMlsGroupBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BootstrapMlsGroupBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BootstrapMlsGroupBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/commit": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CommitMlsGroupBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CommitMlsGroupBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CommitMlsGroupBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/welcome/fanout": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsWelcomeBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsWelcomeBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsWelcomeBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/reshare-required": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/MarkMlsReshareRequiredBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/MarkMlsReshareRequiredBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/MarkMlsReshareRequiredBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsDeviceMembership"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsDeviceMembership"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsDeviceMembership"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/devices/me/reshare-required": {
      "get": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMlsDeviceMembership"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMlsDeviceMembership"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMlsDeviceMembership"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/devices/me/reshare-required/{groupId}/complete": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/groupinfo": {
      "put": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UploadGroupInfoBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UploadGroupInfoBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UploadGroupInfoBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "get": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/e2ee/mls/messages/fanout": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutEnvelopeBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutEnvelopeBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutEnvelopeBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/commit/fanout": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsCommitBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsCommitBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsCommitBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/messages/fanout": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsGroupMessageBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsGroupMessageBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/FanoutMlsGroupMessageBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/envelopes/pending": {
      "get": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 100
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnE2eeEnvelope"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/envelopes/{envelopeId}/ack": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "envelopeId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "X-Device-Id",
            "in": "header",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnE2eeEnvelope"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnE2eeEnvelope"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnE2eeEnvelope"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/devices/{deviceId}/revoke": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "deviceId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/e2ee/mls/devices/{deviceId}/membership": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "deviceId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AddMlsDeviceMembershipBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AddMlsDeviceMembershipBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AddMlsDeviceMembershipBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsDeviceMembership"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsDeviceMembership"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsDeviceMembership"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/e2ee/mls/groups/{groupId}/reset": {
      "post": {
        "tags": [
          "E2Ee"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ResetMlsGroupBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ResetMlsGroupBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ResetMlsGroupBody"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMlsGroupState"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/login/{provider}": {
      "get": {
        "tags": [
          "Oidc"
        ],
        "parameters": [
          {
            "name": "provider",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "returnUrl",
            "in": "query",
            "schema": {
              "type": "string",
              "default": "/"
            }
          },
          {
            "name": "deviceId",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "flow",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/login/apple/mobile": {
      "post": {
        "tags": [
          "Oidc"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AppleMobileSignInRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AppleMobileSignInRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AppleMobileSignInRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/TokenExchangeResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/open/authorize": {
      "get": {
        "tags": [
          "OidcProvider"
        ],
        "parameters": [
          {
            "name": "client_id",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "response_type",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "redirect_uri",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "scope",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "state",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "response_mode",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "nonce",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "display",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "prompt",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "code_challenge",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "code_challenge_method",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "OidcProvider"
        ],
        "requestBody": {
          "content": {
            "application/x-www-form-urlencoded": {
              "schema": {
                "type": "object",
                "properties": {
                  "authorize": {
                    "type": "string"
                  },
                  "client_id": {
                    "type": "string"
                  },
                  "redirect_uri": {
                    "type": "string"
                  },
                  "scope": {
                    "type": "string"
                  },
                  "state": {
                    "type": "string"
                  },
                  "nonce": {
                    "type": "string"
                  },
                  "code_challenge": {
                    "type": "string"
                  },
                  "code_challenge_method": {
                    "type": "string"
                  }
                }
              },
              "encoding": {
                "authorize": {
                  "style": "form"
                },
                "client_id": {
                  "style": "form"
                },
                "redirect_uri": {
                  "style": "form"
                },
                "scope": {
                  "style": "form"
                },
                "state": {
                  "style": "form"
                },
                "nonce": {
                  "style": "form"
                },
                "code_challenge": {
                  "style": "form"
                },
                "code_challenge_method": {
                  "style": "form"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/open/token": {
      "post": {
        "tags": [
          "OidcProvider"
        ],
        "requestBody": {
          "content": {
            "application/x-www-form-urlencoded": {
              "schema": {
                "type": "object",
                "properties": {
                  "grant_type": {
                    "type": "string"
                  },
                  "code": {
                    "type": "string"
                  },
                  "redirect_uri": {
                    "type": "string"
                  },
                  "client_id": {
                    "type": "string"
                  },
                  "client_secret": {
                    "type": "string"
                  },
                  "refresh_token": {
                    "type": "string"
                  },
                  "scope": {
                    "type": "string"
                  },
                  "code_verifier": {
                    "type": "string"
                  },
                  "device_code": {
                    "type": "string"
                  }
                }
              },
              "encoding": {
                "grant_type": {
                  "style": "form"
                },
                "code": {
                  "style": "form"
                },
                "redirect_uri": {
                  "style": "form"
                },
                "client_id": {
                  "style": "form"
                },
                "client_secret": {
                  "style": "form"
                },
                "refresh_token": {
                  "style": "form"
                },
                "scope": {
                  "style": "form"
                },
                "code_verifier": {
                  "style": "form"
                },
                "device_code": {
                  "style": "form"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/open/userinfo": {
      "get": {
        "tags": [
          "OidcProvider"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/.well-known/openid-configuration": {
      "get": {
        "tags": [
          "OidcProvider"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/.well-known/jwks": {
      "get": {
        "tags": [
          "OidcProvider"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/open/device/code": {
      "post": {
        "tags": [
          "OidcProvider"
        ],
        "requestBody": {
          "content": {
            "application/x-www-form-urlencoded": {
              "schema": {
                "required": [
                  "client_id"
                ],
                "type": "object",
                "properties": {
                  "client_id": {
                    "type": "string"
                  },
                  "scope": {
                    "type": "string"
                  },
                  "nonce": {
                    "type": "string"
                  }
                }
              },
              "encoding": {
                "client_id": {
                  "style": "form"
                },
                "scope": {
                  "style": "form"
                },
                "nonce": {
                  "style": "form"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/open/device/code/{userCode}": {
      "get": {
        "tags": [
          "OidcProvider"
        ],
        "parameters": [
          {
            "name": "userCode",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/open/device/code/{userCode}/approve": {
      "post": {
        "tags": [
          "OidcProvider"
        ],
        "parameters": [
          {
            "name": "userCode",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/open/device/code/{userCode}/decline": {
      "post": {
        "tags": [
          "OidcProvider"
        ],
        "parameters": [
          {
            "name": "userCode",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/permissions/check/{actor}/{key}": {
      "get": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "key",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/PermissionNodeActorType"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "boolean"
                }
              },
              "application/json": {
                "schema": {
                  "type": "boolean"
                }
              },
              "text/json": {
                "schema": {
                  "type": "boolean"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      }
    },
    "/padlock/permissions/actors/{actor}/permissions/effective": {
      "get": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionNode"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionNode"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionNode"
                  }
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      }
    },
    "/padlock/permissions/actors/{actor}/permissions/direct": {
      "get": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionNode"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionNode"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionNode"
                  }
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      }
    },
    "/padlock/permissions/actors/{actor}/permissions/{key}": {
      "post": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "key",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PermissionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PermissionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PermissionRequest"
              }
            }
          }
        },
        "responses": {
          "201": {
            "description": "Created",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionNode"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionNode"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionNode"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      },
      "delete": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "key",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/PermissionNodeActorType"
            }
          }
        ],
        "responses": {
          "204": {
            "description": "No Content"
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      }
    },
    "/padlock/permissions/actors/{actor}/groups": {
      "get": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionGroupMember"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionGroupMember"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPermissionGroupMember"
                  }
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      }
    },
    "/padlock/permissions/actors/{actor}/groups/{groupId}": {
      "post": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/GroupMembershipRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/GroupMembershipRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/GroupMembershipRequest"
              }
            }
          }
        },
        "responses": {
          "201": {
            "description": "Created",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroupMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroupMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroupMember"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      },
      "delete": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "204": {
            "description": "No Content"
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      }
    },
    "/padlock/permissions/actors/{actor}/cache/clear": {
      "post": {
        "tags": [
          "Permission"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "204": {
            "description": "No Content"
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "500": {
            "description": "Internal Server Error"
          }
        }
      }
    },
    "/padlock/permissions/validate-pattern": {
      "post": {
        "tags": [
          "Permission"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PatternValidationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PatternValidationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PatternValidationRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PatternValidationResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PatternValidationResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PatternValidationResponse"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/permissions/groups": {
      "get": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PermissionGroupSummary"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PermissionGroupSummary"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PermissionGroupSummary"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PermissionAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePermissionGroupRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePermissionGroupRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePermissionGroupRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroup"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroup"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroup"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/admin/permissions/groups/{groupId}": {
      "get": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PermissionGroupDetailResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PermissionGroupDetailResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PermissionGroupDetailResponse"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePermissionGroupRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePermissionGroupRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdatePermissionGroupRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroup"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroup"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroup"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/permissions/groups/{groupId}/permissions/{key}": {
      "put": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "key",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpsertGroupPermissionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpsertGroupPermissionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpsertGroupPermissionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionNode"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionNode"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionNode"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "key",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/permissions/groups/{groupId}/members/{actor}": {
      "put": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/GroupMembershipRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/GroupMembershipRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/GroupMembershipRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroupMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroupMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPermissionGroupMember"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "groupId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/admin/permissions/actors/{actor}": {
      "get": {
        "tags": [
          "PermissionAdmin"
        ],
        "parameters": [
          {
            "name": "actor",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AdminActorPermissionsResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminActorPermissionsResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminActorPermissionsResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/qr/generate": {
      "post": {
        "tags": [
          "QrLogin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/QrGenerateRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/QrGenerateRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/QrGenerateRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/QrGenerateResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/QrGenerateResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/QrGenerateResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/qr/{id}": {
      "get": {
        "tags": [
          "QrLogin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/QrStatusResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/QrStatusResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/QrStatusResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/auth/qr/{id}/scan": {
      "post": {
        "tags": [
          "QrLogin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/qr/{id}/approve": {
      "post": {
        "tags": [
          "QrLogin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/qr/{id}/decline": {
      "post": {
        "tags": [
          "QrLogin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/auth/webauthn/config": {
      "get": {
        "tags": [
          "WebAuthnDiscovery"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/WebAuthnConfigurationResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/WebAuthnConfigurationResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/WebAuthnConfigurationResponse"
                }
              }
            }
          }
        }
      }
    },
    "/padlock/.well-known/webauthn": {
      "get": {
        "tags": [
          "WebAuthnDiscovery"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/.well-known/permissions": {
      "get": {
        "tags": [
          "WellKnown"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/padlock/.well-known/error-codes": {
      "get": {
        "tags": [
          "WellKnown"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "AccountAuthFactorSummary": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/AccountAuthFactorType"
          },
          "trustworthy": {
            "type": "integer",
            "format": "int32"
          },
          "has_secret": {
            "type": "boolean"
          },
          "config": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "enabled_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "AccountAuthFactorType": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6,
          7,
          8
        ],
        "type": "integer",
        "format": "int32"
      },
      "AccountContactRequest": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/AccountContactType"
          },
          "content": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AccountContactType": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "AccountCreateRequest": {
        "required": [
          "captcha_token",
          "email",
          "name",
          "nick",
          "password"
        ],
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "minLength": 2,
            "pattern": "^[A-Za-z0-9_-]+$",
            "type": "string"
          },
          "nick": {
            "maxLength": 256,
            "minLength": 1,
            "type": "string"
          },
          "email": {
            "maxLength": 1024,
            "minLength": 1,
            "pattern": "^[^+]+@[^@]+\\.[^@]+$",
            "type": "string",
            "format": "email"
          },
          "password": {
            "maxLength": 128,
            "minLength": 4,
            "type": "string"
          },
          "language": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "captcha_token": {
            "minLength": 1,
            "type": "string"
          },
          "affiliation_spell": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AccountCreateValidateRequest": {
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "minLength": 2,
            "pattern": "^[A-Za-z0-9_-]+$",
            "type": "string",
            "nullable": true
          },
          "email": {
            "maxLength": 1024,
            "pattern": "^[^+]+@[^@]+\\.[^@]+$",
            "type": "string",
            "format": "email",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AccountGeographyBucket": {
        "type": "object",
        "properties": {
          "country_code": {
            "type": "string",
            "nullable": true
          },
          "country": {
            "type": "string",
            "nullable": true
          },
          "city": {
            "type": "string",
            "nullable": true
          },
          "latitude": {
            "type": "number",
            "format": "double"
          },
          "longitude": {
            "type": "number",
            "format": "double"
          },
          "user_count": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "AccountGeographyStatsResponse": {
        "type": "object",
        "properties": {
          "calculated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "since": {
            "$ref": "#/components/schemas/Instant"
          },
          "precision": {
            "type": "string",
            "nullable": true
          },
          "accounts_with_location": {
            "type": "integer",
            "format": "int64"
          },
          "buckets": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/AccountGeographyBucket"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AccountPunishmentResponse": {
        "type": "object",
        "properties": {
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "punishments": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountPunishment"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AddMlsDeviceMembershipBody": {
        "required": [
          "epoch",
          "group_id"
        ],
        "type": "object",
        "properties": {
          "group_id": {
            "minLength": 1,
            "type": "string"
          },
          "epoch": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "AdminAccountAuthFactorRequest": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/AccountAuthFactorType"
          },
          "secret": {
            "type": "string",
            "nullable": true
          },
          "enable": {
            "type": "boolean"
          },
          "code": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminAccountContactRequest": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/AccountContactType"
          },
          "content": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminAccountDetailResponse": {
        "type": "object",
        "properties": {
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "contacts": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountContact"
            },
            "nullable": true
          },
          "auth_factors": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/AccountAuthFactorSummary"
            },
            "nullable": true
          },
          "active_session_count": {
            "type": "integer",
            "format": "int32"
          },
          "active_device_count": {
            "type": "integer",
            "format": "int32"
          },
          "active_punishment": {
            "$ref": "#/components/schemas/SnAccountPunishment"
          },
          "active_punishments": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountPunishment"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminAccountSummaryResponse": {
        "type": "object",
        "properties": {
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "primary_email": {
            "type": "string",
            "nullable": true
          },
          "contact_count": {
            "type": "integer",
            "format": "int32"
          },
          "auth_factor_count": {
            "type": "integer",
            "format": "int32"
          },
          "has_password": {
            "type": "boolean"
          },
          "active_session_count": {
            "type": "integer",
            "format": "int32"
          },
          "active_device_count": {
            "type": "integer",
            "format": "int32"
          },
          "active_punishment": {
            "$ref": "#/components/schemas/SnAccountPunishment"
          }
        },
        "additionalProperties": false
      },
      "AdminActorPermissionsResponse": {
        "type": "object",
        "properties": {
          "actor": {
            "type": "string",
            "nullable": true
          },
          "direct_permissions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPermissionNode"
            },
            "nullable": true
          },
          "effective_permissions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPermissionNode"
            },
            "nullable": true
          },
          "groups": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPermissionGroupMember"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminContactVerificationRequest": {
        "type": "object",
        "properties": {
          "verified_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "AdminMessageDispatchResponse": {
        "type": "object",
        "properties": {
          "requested": {
            "type": "integer",
            "format": "int32"
          },
          "resolved": {
            "type": "integer",
            "format": "int32"
          },
          "sent": {
            "type": "integer",
            "format": "int32"
          },
          "skipped": {
            "type": "integer",
            "format": "int32"
          },
          "broadcast_to_all": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "AdminResetPasswordFactorRequest": {
        "type": "object",
        "properties": {
          "new_password": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "revoke_sessions": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "AppleMobileConnectRequest": {
        "required": [
          "authorization_code",
          "identity_token"
        ],
        "type": "object",
        "properties": {
          "identity_token": {
            "minLength": 1,
            "type": "string"
          },
          "authorization_code": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "AppleMobileSignInRequest": {
        "required": [
          "authorization_code",
          "device_id",
          "identity_token"
        ],
        "type": "object",
        "properties": {
          "identity_token": {
            "minLength": 1,
            "type": "string"
          },
          "authorization_code": {
            "minLength": 1,
            "type": "string"
          },
          "device_id": {
            "maxLength": 512,
            "minLength": 1,
            "type": "string"
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AuthFactorRequest": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/AccountAuthFactorType"
          },
          "secret": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AuthenticatorSelectionCriteria": {
        "type": "object",
        "properties": {
          "authenticator_attachment": {
            "type": "string",
            "nullable": true
          },
          "resident_key": {
            "type": "string",
            "nullable": true
          },
          "user_verification": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AuthorizeAppScopesRequest": {
        "type": "object",
        "properties": {
          "scopes": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AuthorizedAppResponse": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "app_id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/AuthorizedAppType"
          },
          "app_slug": {
            "type": "string",
            "nullable": true
          },
          "app_name": {
            "type": "string",
            "nullable": true
          },
          "app_description": {
            "type": "string",
            "nullable": true
          },
          "picture": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "scopes": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "last_authorized_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_used_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "AuthorizedAppType": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "BasicInfoRequest": {
        "type": "object",
        "properties": {
          "nick": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "language": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "region": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BatchCheckMlsReadyRequest": {
        "required": [
          "account_ids"
        ],
        "type": "object",
        "properties": {
          "account_ids": {
            "maxItems": 100,
            "minItems": 1,
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            }
          }
        },
        "additionalProperties": false
      },
      "BatchCheckMlsReadyResponse": {
        "type": "object",
        "properties": {
          "users": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/MlsUserAvailability"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BootstrapMlsGroupBody": {
        "required": [
          "epoch"
        ],
        "type": "object",
        "properties": {
          "epoch": {
            "type": "integer",
            "format": "int64"
          },
          "state_version": {
            "type": "integer",
            "format": "int64"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CacheClearResponse": {
        "type": "object",
        "properties": {
          "scope": {
            "type": "string",
            "nullable": true
          },
          "key": {
            "type": "string",
            "nullable": true
          },
          "group": {
            "type": "string",
            "nullable": true
          },
          "removed_count": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "CacheGroupResponse": {
        "type": "object",
        "properties": {
          "group": {
            "type": "string",
            "nullable": true
          },
          "count": {
            "type": "integer",
            "format": "int32"
          },
          "keys": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CacheStatsSnapshot": {
        "type": "object",
        "properties": {
          "keyspace_hits": {
            "type": "integer",
            "format": "int64"
          },
          "keyspace_misses": {
            "type": "integer",
            "format": "int64"
          },
          "total_commands_processed": {
            "type": "integer",
            "format": "int64"
          },
          "evicted_keys": {
            "type": "integer",
            "format": "int64"
          },
          "expired_keys": {
            "type": "integer",
            "format": "int64"
          },
          "connected_clients": {
            "type": "integer",
            "format": "int64"
          },
          "used_memory_bytes": {
            "type": "integer",
            "format": "int64"
          },
          "read_count": {
            "type": "integer",
            "format": "int64",
            "readOnly": true
          },
          "hit_ratio": {
            "type": "number",
            "format": "double",
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "CaptchaVerifyRequest": {
        "type": "object",
        "properties": {
          "token": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ChallengeRequest": {
        "required": [
          "account",
          "device_id",
          "platform"
        ],
        "type": "object",
        "properties": {
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          },
          "account": {
            "maxLength": 256,
            "minLength": 1,
            "type": "string"
          },
          "device_id": {
            "maxLength": 512,
            "minLength": 1,
            "type": "string"
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "audiences": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "scopes": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CheckMlsReadyResponse": {
        "type": "object",
        "properties": {
          "is_ready": {
            "type": "boolean"
          },
          "available_key_packages": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "ClearGroupRequest": {
        "required": [
          "group"
        ],
        "type": "object",
        "properties": {
          "group": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "ClearKeyRequest": {
        "required": [
          "key"
        ],
        "type": "object",
        "properties": {
          "key": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "ClientPlatform": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6
        ],
        "type": "integer",
        "format": "int32"
      },
      "CommitMlsGroupBody": {
        "required": [
          "epoch"
        ],
        "type": "object",
        "properties": {
          "epoch": {
            "type": "integer",
            "format": "int64"
          },
          "reason": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ContentSensitiveMark": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6,
          7,
          8,
          9,
          10,
          11,
          12
        ],
        "type": "integer",
        "format": "int32"
      },
      "CreateApiKeyRequest": {
        "type": "object",
        "properties": {
          "label": {
            "type": "string",
            "nullable": true
          },
          "expired_at": {
            "type": "string",
            "format": "date-time",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreatePermissionGroupRequest": {
        "required": [
          "key"
        ],
        "type": "object",
        "properties": {
          "key": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "CreatePunishmentRequest": {
        "type": "object",
        "properties": {
          "reason": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "type": {
            "$ref": "#/components/schemas/PunishmentType"
          },
          "blocked_permissions": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "social_credit_reduction": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "publisher_rating_reduction": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "publisher_names": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "FanoutEnvelopeBody": {
        "required": [
          "payloads",
          "recipient_account_id"
        ],
        "type": "object",
        "properties": {
          "recipient_account_id": {
            "type": "string",
            "format": "uuid"
          },
          "session_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/SnE2eeEnvelopeType"
          },
          "group_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "expires_at": {
            "type": "string",
            "format": "date-time",
            "nullable": true
          },
          "include_sender_copy": {
            "type": "boolean"
          },
          "payloads": {
            "minItems": 1,
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/FanoutEnvelopeItemBody"
            }
          }
        },
        "additionalProperties": false
      },
      "FanoutEnvelopeItemBody": {
        "required": [
          "ciphertext"
        ],
        "type": "object",
        "properties": {
          "recipient_device_id": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "client_message_id": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "ciphertext": {
            "type": "string",
            "format": "byte"
          },
          "header": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "signature": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "FanoutMlsCommitBody": {
        "required": [
          "ciphertext",
          "epoch"
        ],
        "type": "object",
        "properties": {
          "epoch": {
            "type": "integer",
            "format": "int64"
          },
          "ciphertext": {
            "type": "string",
            "format": "byte"
          },
          "header": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "signature": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "client_message_id": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "FanoutMlsGroupMessageBody": {
        "required": [
          "ciphertext"
        ],
        "type": "object",
        "properties": {
          "ciphertext": {
            "type": "string",
            "format": "byte"
          },
          "header": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "signature": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "client_message_id": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "FanoutMlsWelcomeBody": {
        "required": [
          "payloads"
        ],
        "type": "object",
        "properties": {
          "recipient_account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "expires_at": {
            "type": "string",
            "format": "date-time",
            "nullable": true
          },
          "payloads": {
            "minItems": 1,
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/FanoutEnvelopeItemBody"
            }
          }
        },
        "additionalProperties": false
      },
      "GeoPoint": {
        "type": "object",
        "properties": {
          "latitude": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "longitude": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "country_code": {
            "type": "string",
            "nullable": true
          },
          "country": {
            "type": "string",
            "nullable": true
          },
          "city": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "GroupMembershipRequest": {
        "type": "object",
        "properties": {
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "affected_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "Instant": {
        "type": "object",
        "additionalProperties": false
      },
      "MarkMlsReshareRequiredBody": {
        "required": [
          "epoch",
          "reason",
          "target_account_id",
          "target_device_id"
        ],
        "type": "object",
        "properties": {
          "target_account_id": {
            "type": "string",
            "format": "uuid"
          },
          "target_device_id": {
            "maxLength": 512,
            "minLength": 1,
            "type": "string"
          },
          "epoch": {
            "type": "integer",
            "format": "int64"
          },
          "reason": {
            "maxLength": 128,
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "MlsDeviceKeyPackageResponse": {
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "device_id": {
            "type": "string",
            "nullable": true
          },
          "device_label": {
            "type": "string",
            "nullable": true
          },
          "ciphersuite": {
            "type": "string",
            "nullable": true
          },
          "key_package": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "MlsDeviceKpStatus": {
        "type": "object",
        "properties": {
          "device_id": {
            "type": "string",
            "nullable": true
          },
          "device_label": {
            "type": "string",
            "nullable": true
          },
          "available_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "MlsKeyPackageStatusResponse": {
        "type": "object",
        "properties": {
          "needs_more_kps": {
            "type": "boolean"
          },
          "devices_needing_kps": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/MlsDeviceKpStatus"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "MlsUserAvailability": {
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "is_ready": {
            "type": "boolean"
          },
          "available_key_packages": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "NewSessionRequest": {
        "required": [
          "device_id",
          "platform"
        ],
        "type": "object",
        "properties": {
          "device_id": {
            "maxLength": 512,
            "minLength": 1,
            "type": "string"
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "PasskeyAuthenticationCompleteRequest": {
        "required": [
          "authenticator_data",
          "client_data_json",
          "credential_id",
          "signature"
        ],
        "type": "object",
        "properties": {
          "credential_id": {
            "minLength": 1,
            "type": "string"
          },
          "client_data_json": {
            "minLength": 1,
            "type": "string"
          },
          "authenticator_data": {
            "minLength": 1,
            "type": "string"
          },
          "signature": {
            "minLength": 1,
            "type": "string"
          },
          "user_handle": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PasskeyAuthenticationStartResponse": {
        "type": "object",
        "properties": {
          "challenge": {
            "type": "string",
            "nullable": true
          },
          "rp_id": {
            "type": "string",
            "nullable": true
          },
          "allow_credentials": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/PasskeyCredentialDescriptor"
            },
            "nullable": true
          },
          "timeout": {
            "type": "integer",
            "format": "int32"
          },
          "user_verification": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PasskeyCredentialDescriptor": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "id": {
            "type": "string",
            "nullable": true
          },
          "transports": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PasskeyLoginStartRequest": {
        "required": [
          "device_id",
          "platform"
        ],
        "type": "object",
        "properties": {
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          },
          "device_id": {
            "maxLength": 512,
            "minLength": 1,
            "type": "string"
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "audiences": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "scopes": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PasskeyLoginStartResponse": {
        "type": "object",
        "properties": {
          "challenge": {
            "type": "string",
            "nullable": true
          },
          "rp_id": {
            "type": "string",
            "nullable": true
          },
          "allow_credentials": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/PasskeyCredentialDescriptor"
            },
            "nullable": true
          },
          "timeout": {
            "type": "integer",
            "format": "int32"
          },
          "user_verification": {
            "type": "string",
            "nullable": true
          },
          "auth_challenge_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "PasskeyRegistrationCompleteRequest": {
        "type": "object",
        "properties": {
          "device_id": {
            "type": "string",
            "nullable": true
          },
          "client_data_json": {
            "type": "string",
            "nullable": true
          },
          "attestation_object": {
            "type": "string",
            "nullable": true
          },
          "label": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PasskeyRegistrationStartRequest": {
        "type": "object",
        "properties": {
          "device_id": {
            "type": "string",
            "nullable": true
          },
          "device_name": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PasskeyRegistrationStartResponse": {
        "type": "object",
        "properties": {
          "challenge": {
            "type": "string",
            "nullable": true
          },
          "rp_id": {
            "type": "string",
            "nullable": true
          },
          "rp_name": {
            "type": "string",
            "nullable": true
          },
          "user_id": {
            "type": "string",
            "nullable": true
          },
          "user_name": {
            "type": "string",
            "nullable": true
          },
          "display_name": {
            "type": "string",
            "nullable": true
          },
          "pub_key_cred_params": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/PublicKeyCredentialParameters"
            },
            "nullable": true
          },
          "timeout": {
            "type": "integer",
            "format": "int32"
          },
          "authenticator_selection": {
            "$ref": "#/components/schemas/AuthenticatorSelectionCriteria"
          }
        },
        "additionalProperties": false
      },
      "PatternValidationRequest": {
        "type": "object",
        "properties": {
          "pattern": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PatternValidationResponse": {
        "type": "object",
        "properties": {
          "pattern": {
            "type": "string",
            "nullable": true
          },
          "is_valid": {
            "type": "boolean"
          },
          "message": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PerformChallengeRequest": {
        "required": [
          "factor_id",
          "password"
        ],
        "type": "object",
        "properties": {
          "factor_id": {
            "type": "string",
            "format": "uuid"
          },
          "password": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "PermissionGroupDetailResponse": {
        "type": "object",
        "properties": {
          "group": {
            "$ref": "#/components/schemas/SnPermissionGroup"
          },
          "nodes": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPermissionNode"
            },
            "nullable": true
          },
          "members": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPermissionGroupMember"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PermissionGroupSummary": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "key": {
            "type": "string",
            "nullable": true
          },
          "node_count": {
            "type": "integer",
            "format": "int32"
          },
          "member_count": {
            "type": "integer",
            "format": "int32"
          },
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "PermissionNodeActorType": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "PermissionRequest": {
        "type": "object",
        "properties": {
          "value": {
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "affected_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "PinStatusResponse": {
        "type": "object",
        "properties": {
          "has_pin": {
            "type": "boolean"
          },
          "validation_required": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "ProblemDetails": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "status": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "detail": {
            "type": "string",
            "nullable": true
          },
          "instance": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": { }
      },
      "PublicKeyCredentialParameters": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "alg": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "PublishMlsKeyPackageBody": {
        "required": [
          "device_id",
          "key_package"
        ],
        "type": "object",
        "properties": {
          "key_package": {
            "type": "string",
            "format": "byte"
          },
          "ciphersuite": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "device_id": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          },
          "device_label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PunishmentType": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "QrGenerateRequest": {
        "required": [
          "device_id",
          "platform"
        ],
        "type": "object",
        "properties": {
          "device_id": {
            "maxLength": 512,
            "minLength": 1,
            "type": "string"
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          },
          "audiences": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "scopes": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "QrGenerateResponse": {
        "type": "object",
        "properties": {
          "qr_challenge_id": {
            "type": "string",
            "format": "uuid"
          },
          "auth_challenge_id": {
            "type": "string",
            "format": "uuid"
          },
          "qr_data": {
            "type": "string",
            "nullable": true
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expires_in_seconds": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "QrLoginStatus": {
        "enum": [
          0,
          1,
          2,
          3,
          4
        ],
        "type": "integer",
        "format": "int32"
      },
      "QrStatusResponse": {
        "type": "object",
        "properties": {
          "qr_challenge_id": {
            "type": "string",
            "format": "uuid"
          },
          "auth_challenge_id": {
            "type": "string",
            "format": "uuid"
          },
          "status": {
            "$ref": "#/components/schemas/QrLoginStatus"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "approved_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "approved_device_id": {
            "type": "string",
            "nullable": true
          },
          "device_name": {
            "type": "string",
            "nullable": true
          },
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          }
        },
        "additionalProperties": false
      },
      "RecoveryRequest": {
        "required": [
          "account",
          "captcha_token",
          "device_id",
          "recovery_code"
        ],
        "type": "object",
        "properties": {
          "account": {
            "minLength": 1,
            "type": "string"
          },
          "recovery_code": {
            "minLength": 1,
            "type": "string"
          },
          "captcha_token": {
            "minLength": 1,
            "type": "string"
          },
          "device_id": {
            "minLength": 1,
            "type": "string"
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          }
        },
        "additionalProperties": false
      },
      "ResetMlsGroupBody": {
        "type": "object",
        "properties": {
          "new_epoch": {
            "type": "integer",
            "format": "int64"
          },
          "state_version": {
            "type": "integer",
            "format": "int64"
          },
          "reason": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SendAdminEmailRequest": {
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "account_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "broadcast_to_all": {
            "type": "boolean"
          },
          "subject": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "html_body": {
            "maxLength": 1000000,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SendAdminNotificationRequest": {
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "account_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "broadcast_to_all": {
            "type": "boolean"
          },
          "topic": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "subtitle": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "body": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "action_uri": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "push_type": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "is_silent": {
            "type": "boolean"
          },
          "is_savable": {
            "type": "boolean"
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SendCustomAppNotificationRequest": {
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "account_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "broadcast_to_all": {
            "type": "boolean"
          },
          "topic": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "subtitle": {
            "type": "string",
            "nullable": true
          },
          "body": {
            "type": "string",
            "nullable": true
          },
          "action_uri": {
            "type": "string",
            "nullable": true
          },
          "push_type": {
            "type": "string",
            "nullable": true
          },
          "is_silent": {
            "type": "boolean"
          },
          "is_savable": {
            "type": "boolean"
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SessionType": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "SetAdminContactVisibilityRequest": {
        "type": "object",
        "properties": {
          "is_public": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SetConnectionVisibilityRequest": {
        "type": "object",
        "properties": {
          "is_public": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SnAccount": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "nick": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "language": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "region": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_superuser": {
            "type": "boolean"
          },
          "automated_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "profile": {
            "$ref": "#/components/schemas/SnAccountProfile"
          },
          "contacts": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountContact"
            },
            "nullable": true
          },
          "badges": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountBadge"
            },
            "nullable": true
          },
          "perk_subscription": {
            "$ref": "#/components/schemas/SnSubscriptionReferenceObject"
          },
          "perk_level": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SnAccountAuthFactor": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/AccountAuthFactorType"
          },
          "trustworthy": {
            "type": "integer",
            "format": "int32"
          },
          "enabled_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "created_response": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountBadge": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "caption": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountBadgeRef": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "label": {
            "type": "string",
            "nullable": true
          },
          "caption": {
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountBoardItem": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "kind": {
            "$ref": "#/components/schemas/SnAccountBoardItemKind"
          },
          "widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "custom_app_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "custom_app_widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "is_enabled": {
            "type": "boolean"
          },
          "payload": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountBoardItemKind": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "SnAccountConnection": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "provider": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "provided_identifier": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "last_used_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_public": {
            "type": "boolean"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnAccountContact": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/AccountContactType"
          },
          "verified_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_primary": {
            "type": "boolean"
          },
          "is_public": {
            "type": "boolean"
          },
          "content": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountPasskey": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "label": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountProfile": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "first_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "middle_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "last_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "gender": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "pronouns": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "time_zone": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "links": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnProfileLink"
            },
            "nullable": true
          },
          "username_color": {
            "$ref": "#/components/schemas/UsernameColor"
          },
          "birthday": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_seen_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "verification": {
            "$ref": "#/components/schemas/SnVerificationMark"
          },
          "active_badge": {
            "$ref": "#/components/schemas/SnAccountBadgeRef"
          },
          "experience": {
            "type": "integer",
            "format": "int32"
          },
          "level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "leveling_progress": {
            "type": "number",
            "format": "double",
            "readOnly": true
          },
          "social_credits": {
            "type": "number",
            "format": "double"
          },
          "social_credits_level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "picture": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "board": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountBoardItem"
            },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountPunishment": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "reason": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "type": {
            "$ref": "#/components/schemas/PunishmentType"
          },
          "blocked_permissions": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "creator_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "creator": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnActionLog": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "action": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "user_agent": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "ip_address": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "location": {
            "$ref": "#/components/schemas/GeoPoint"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "session_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnAuthChallenge": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "step_remain": {
            "type": "integer",
            "format": "int32"
          },
          "step_total": {
            "type": "integer",
            "format": "int32"
          },
          "failed_attempts": {
            "type": "integer",
            "format": "int32"
          },
          "blacklist_factors": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "audiences": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "scopes": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "ip_address": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "user_agent": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "device_id": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          },
          "nonce": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location": {
            "$ref": "#/components/schemas/GeoPoint"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "approved_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "declined_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "approved_by_session_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnAuthClient": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "device_label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "device_id": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAuthClientWithSessions": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "platform": {
            "$ref": "#/components/schemas/ClientPlatform"
          },
          "device_name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "device_label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "device_id": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "sessions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAuthSession"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnAuthSession": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/SessionType"
          },
          "last_granted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "audiences": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "scopes": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "ip_address": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "user_agent": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "location": {
            "$ref": "#/components/schemas/GeoPoint"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "client_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "client": {
            "$ref": "#/components/schemas/SnAuthClient"
          },
          "parent_session_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "parent_session": {
            "$ref": "#/components/schemas/SnAuthSession"
          },
          "children_count": {
            "type": "integer",
            "format": "int32"
          },
          "challenge_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "app_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "epoch": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SnCloudFileReferenceObject": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "nullable": true
          },
          "name": {
            "type": "string",
            "nullable": true
          },
          "file_meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "user_meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "sensitive_marks": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ContentSensitiveMark"
            },
            "nullable": true
          },
          "mime_type": {
            "type": "string",
            "nullable": true
          },
          "hash": {
            "type": "string",
            "nullable": true
          },
          "size": {
            "type": "integer",
            "format": "int64"
          },
          "has_compression": {
            "type": "boolean"
          },
          "url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "width": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "height": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "blurhash": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "usage": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "application_type": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnE2eeEnvelope": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "sender_id": {
            "type": "string",
            "format": "uuid"
          },
          "sender_device_id": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "recipient_id": {
            "type": "string",
            "format": "uuid"
          },
          "recipient_account_id": {
            "type": "string",
            "format": "uuid"
          },
          "recipient_device_id": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "session_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/SnE2eeEnvelopeType"
          },
          "group_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "client_message_id": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "sequence": {
            "type": "integer",
            "format": "int64"
          },
          "ciphertext": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "header": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "signature": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "delivery_status": {
            "$ref": "#/components/schemas/SnE2eeEnvelopeStatus"
          },
          "delivered_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "acked_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "legacy_account_scoped": {
            "type": "boolean"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnE2eeEnvelopeStatus": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "SnE2eeEnvelopeType": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6,
          7
        ],
        "type": "integer",
        "format": "int32"
      },
      "SnMlsDeviceMembership": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "mls_group_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "device_id": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "joined_epoch": {
            "type": "integer",
            "format": "int64"
          },
          "last_seen_epoch": {
            "type": "integer",
            "format": "int64",
            "nullable": true
          },
          "last_reshare_required_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_reshare_completed_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnMlsGroupState": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "mls_group_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "epoch": {
            "type": "integer",
            "format": "int64"
          },
          "state_version": {
            "type": "integer",
            "format": "int64"
          },
          "last_commit_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "group_info": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "ratchet_tree": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnMlsKeyPackage": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "device_id": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "device_label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "key_package": {
            "type": "string",
            "format": "byte",
            "nullable": true
          },
          "ciphersuite": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "is_consumed": {
            "type": "boolean"
          },
          "consumed_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "consumed_by_account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnPermissionGroup": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "key": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "nodes": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPermissionNode"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnPermissionGroupMember": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "group_id": {
            "type": "string",
            "format": "uuid"
          },
          "group": {
            "$ref": "#/components/schemas/SnPermissionGroup"
          },
          "actor": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "affected_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnPermissionNode": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/PermissionNodeActorType"
          },
          "actor": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "key": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "value": {
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "affected_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "group_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnProfileLink": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "url": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnSubscriptionReferenceObject": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "identifier": {
            "type": "string",
            "nullable": true
          },
          "group_identifier": {
            "type": "string",
            "nullable": true
          },
          "display_name": {
            "type": "string",
            "nullable": true
          },
          "perk_level": {
            "type": "integer",
            "format": "int32"
          },
          "is_testing": {
            "type": "boolean"
          },
          "begun_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_active": {
            "type": "boolean"
          },
          "is_available": {
            "type": "boolean"
          },
          "is_pending_activation": {
            "type": "boolean"
          },
          "is_free_trial": {
            "type": "boolean"
          },
          "status": {
            "$ref": "#/components/schemas/SubscriptionStatus"
          },
          "base_price": {
            "type": "number",
            "format": "double"
          },
          "final_price": {
            "type": "number",
            "format": "double"
          },
          "renewal_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnVerificationMark": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/VerificationMarkType"
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "verified_by": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SubscriptionStatus": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "SudoRequest": {
        "type": "object",
        "properties": {
          "pin_code": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SuspendAccountRequest": {
        "type": "object",
        "properties": {
          "reason": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "type": {
            "$ref": "#/components/schemas/PunishmentType"
          },
          "revoke_sessions": {
            "type": "boolean"
          },
          "social_credit_reduction": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "publisher_rating_reduction": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "publisher_names": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "TokenExchangeRequest": {
        "type": "object",
        "properties": {
          "grant_type": {
            "type": "string",
            "nullable": true
          },
          "refresh_token": {
            "type": "string",
            "nullable": true
          },
          "code": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "TokenExchangeResponse": {
        "type": "object",
        "properties": {
          "token": {
            "type": "string",
            "nullable": true
          },
          "refresh_token": {
            "type": "string",
            "nullable": true
          },
          "expires_in": {
            "type": "integer",
            "format": "int64"
          },
          "refresh_expires_in": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "UpdateAdminAccountContactRequest": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/AccountContactType"
          },
          "content": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateAdminDeviceLabelRequest": {
        "type": "object",
        "properties": {
          "label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdatePasskeyRequest": {
        "type": "object",
        "properties": {
          "label": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdatePermissionGroupRequest": {
        "required": [
          "key"
        ],
        "type": "object",
        "properties": {
          "key": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "UpdatePunishmentRequest": {
        "type": "object",
        "properties": {
          "reason": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "type": {
            "$ref": "#/components/schemas/PunishmentType"
          },
          "blocked_permissions": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UploadGroupInfoBody": {
        "required": [
          "epoch",
          "group_info",
          "ratchet_tree"
        ],
        "type": "object",
        "properties": {
          "epoch": {
            "type": "integer",
            "format": "int64"
          },
          "group_info": {
            "type": "string",
            "format": "byte"
          },
          "ratchet_tree": {
            "type": "string",
            "format": "byte"
          }
        },
        "additionalProperties": false
      },
      "UpsertGroupPermissionRequest": {
        "type": "object",
        "properties": {
          "value": {
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "affected_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "UsernameColor": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "value": {
            "type": "string",
            "nullable": true
          },
          "direction": {
            "type": "string",
            "nullable": true
          },
          "colors": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "VerificationMarkType": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6
        ],
        "type": "integer",
        "format": "int32"
      },
      "WebAuthnConfigurationResponse": {
        "type": "object",
        "properties": {
          "rp_id": {
            "type": "string",
            "nullable": true
          },
          "rp_name": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      }
    },
    "securitySchemes": {
      "Bearer": {
        "type": "http",
        "description": "Solar Network Unified Authentication",
        "scheme": "Bearer",
        "bearerFormat": "JWT"
      }
    }
  },
  "tags": [
    {
      "name": "Account"
    },
    {
      "name": "AccountActionLog"
    },
    {
      "name": "AccountAdmin"
    },
    {
      "name": "AccountCurrent"
    },
    {
      "name": "AccountGeographyStatsAdmin"
    },
    {
      "name": "AccountPunishment"
    },
    {
      "name": "AccountSecurity"
    },
    {
      "name": "ApiKey"
    },
    {
      "name": "Auth"
    },
    {
      "name": "Captcha"
    },
    {
      "name": "CacheAdmin"
    },
    {
      "name": "Connection"
    },
    {
      "name": "CustomAppContact"
    },
    {
      "name": "CustomAppNotification"
    },
    {
      "name": "DysonNetwork.Padlock"
    },
    {
      "name": "E2Ee"
    },
    {
      "name": "Oidc"
    },
    {
      "name": "OidcProvider"
    },
    {
      "name": "Permission"
    },
    {
      "name": "PermissionAdmin"
    },
    {
      "name": "QrLogin"
    },
    {
      "name": "WebAuthnDiscovery"
    },
    {
      "name": "WellKnown"
    }
  ]
}
Passport API
DysonNetwork.Passport 是主要负责用户信息的服务，包括签到、个人资料（头像，简介等）； 曾经其也负责登陆、授权等业务，不过在近期的重构被分配到 DysonNetwork.Padlock 服务中去了。

以下是自动生成的 API 文档，作为参考用途:{
  "openapi": "3.0.4",
  "info": {
    "title": "DysonNetwork.Passport",
    "description": "The authentication and authorization service in the Solar Network.",
    "termsOfService": "https://solsynth.dev/terms",
    "license": {
      "name": "APGLv3",
      "url": "https://www.gnu.org/licenses/agpl-3.0.html"
    },
    "version": "v1"
  },
  "paths": {
    "/passport/accounts/recovery/password": {
      "post": {
        "tags": [
          "Account"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RecoveryPasswordRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RecoveryPasswordRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RecoveryPasswordRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/accounts/{name}/statuses": {
      "get": {
        "tags": [
          "Account"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/calendar": {
      "get": {
        "tags": [
          "Account"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "month",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "year",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "includeNotableDays",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DailyEventResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DailyEventResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DailyEventResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/calendar/merged": {
      "get": {
        "tags": [
          "Account"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "month",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "year",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/MergedDailyEventResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/MergedDailyEventResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/MergedDailyEventResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/calendar/countdown": {
      "get": {
        "tags": [
          "Account"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 5
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "includeNotableDays",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": true
            }
          },
          {
            "name": "tag",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EventCountdownItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EventCountdownItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EventCountdownItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/timeline": {
      "get": {
        "tags": [
          "Account"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountTimelineItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountTimelineItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountTimelineItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/calendar/events": {
      "get": {
        "tags": [
          "Account"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "startTime",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "endTime",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnUserCalendarEvent"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnUserCalendarEvent"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnUserCalendarEvent"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/calendar/events/{id}": {
      "get": {
        "tags": [
          "Account"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/unknown/calendar/events/{id}": {
      "get": {
        "tags": [
          "Account"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/metrics/activity": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountActivityMetricsResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountActivityMetricsResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountActivityMetricsResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "orderBy",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AdminAccountSummaryResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AdminAccountSummaryResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AdminAccountSummaryResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountDetailResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountDetailResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminAccountDetailResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/contacts": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountContact"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/spells": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMagicSpell"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMagicSpell"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMagicSpell"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminMagicSpellRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminMagicSpellRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminMagicSpellRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMagicSpell"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMagicSpell"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMagicSpell"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/spells/{spellId}/resend": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "spellId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ResendAdminMagicSpellRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ResendAdminMagicSpellRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ResendAdminMagicSpellRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/spells/{spellId}": {
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "spellId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/factors": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountAuthFactorSummary"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountAuthFactorSummary"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AccountAuthFactorSummary"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/verification": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAccountVerificationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAccountVerificationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAccountVerificationRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnVerificationMark"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnVerificationMark"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnVerificationMark"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/badges": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminBadgeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminBadgeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminBadgeRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBadge"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBadge"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBadge"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/badges/{badgeId}/activate": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "badgeId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/badges/{badgeId}": {
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "badgeId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/board": {
      "get": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/AdminBoardItemRequest"
                }
              }
            },
            "text/json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/AdminBoardItemRequest"
                }
              }
            },
            "application/*+json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/AdminBoardItemRequest"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/board/items/{itemId}/payload": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "itemId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminPushBoardPayloadRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminPushBoardPayloadRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminPushBoardPayloadRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBoardItem"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBoardItem"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBoardItem"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/{identifier}/board/items/{itemId}": {
      "delete": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "itemId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/accounts/{name}/credits": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/accounts/presences/steam/scan": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/presences/steam/scan/{identifier}": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/presences/steam/scan-by-steam-id/{steamId}": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "steamId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/accounts/presences/steam/scan/stages/{stage}": {
      "post": {
        "tags": [
          "AccountAdmin"
        ],
        "parameters": [
          {
            "name": "stage",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SteamPresenceScanResult"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountCurrent"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/accounts/me/passbook/member": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/accounts/me/profile": {
      "patch": {
        "tags": [
          "AccountCurrent"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ProfileRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ProfileRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ProfileRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountProfile"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountProfile"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountProfile"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/board": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "AccountCurrent"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/BoardItemRequest"
                }
              }
            },
            "text/json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/BoardItemRequest"
                }
              }
            },
            "application/*+json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/BoardItemRequest"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/actions": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnActionLog"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnActionLog"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnActionLog"
                  }
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/badges": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/badges/{id}/active": {
      "post": {
        "tags": [
          "AccountCurrent"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBadge"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBadge"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountBadge"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/leveling": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnExperienceRecord"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnExperienceRecord"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnExperienceRecord"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/credits": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "boolean"
                }
              },
              "application/json": {
                "schema": {
                  "type": "boolean"
                }
              },
              "text/json": {
                "schema": {
                  "type": "boolean"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/credits/history": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnSocialCreditRecord"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSocialCreditRecord"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSocialCreditRecord"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/connections": {
      "get": {
        "tags": [
          "AccountCurrent"
        ],
        "parameters": [
          {
            "name": "provider",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountConnection"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountConnection"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountConnection"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/statuses": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "AccountEvent"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/StatusRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StatusRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StatusRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountEvent"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/StatusRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StatusRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StatusRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountStatus"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "app",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/accounts/me/check-in": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "version",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 1
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnCheckInResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnCheckInResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnCheckInResult"
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "backdated",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "version",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 1
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "string"
              }
            },
            "text/json": {
              "schema": {
                "type": "string"
              }
            },
            "application/*+json": {
              "schema": {
                "type": "string"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnCheckInResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnCheckInResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnCheckInResult"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "month",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "year",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "includeNotableDays",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DailyEventResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DailyEventResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DailyEventResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/merged": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "month",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "year",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/MergedDailyEventResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/MergedDailyEventResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/MergedDailyEventResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/tags": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/events": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "tags",
            "in": "query",
            "schema": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          {
            "name": "startTime",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "endTime",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnUserCalendarEvent"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnUserCalendarEvent"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnUserCalendarEvent"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "AccountEvent"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateCalendarEventRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateCalendarEventRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateCalendarEventRequest"
              }
            }
          }
        },
        "responses": {
          "201": {
            "description": "Created",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/events/{id}": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCalendarEventRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCalendarEventRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCalendarEventRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnUserCalendarEvent"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "204": {
            "description": "No Content"
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/notable-days/{occurrenceKey}": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "occurrenceKey",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NotableDay"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NotableDay"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NotableDay"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/search": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "accountId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "tags",
            "in": "query",
            "schema": {
              "type": "array",
              "items": {
                "type": "string"
              }
            }
          },
          {
            "name": "startTime",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "endTime",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "notableDayTag",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "region",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/CalendarSearchResultItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/CalendarSearchResultItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/CalendarSearchResultItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/countdown": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 5
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "includeNotableDays",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": true
            }
          },
          {
            "name": "tag",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EventCountdownItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EventCountdownItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EventCountdownItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/subscriptions": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string",
                    "format": "uuid"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string",
                    "format": "uuid"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string",
                    "format": "uuid"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/subscriptions/{accountId}": {
      "post": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "201": {
            "description": "Created",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnCalendarEventSubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnCalendarEventSubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnCalendarEventSubscription"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AccountEvent"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "204": {
            "description": "No Content"
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/calendar/subscriptions/subscribers": {
      "get": {
        "tags": [
          "AccountEvent"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string",
                    "format": "uuid"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string",
                    "format": "uuid"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "type": "string",
                    "format": "uuid"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}": {
      "get": {
        "tags": [
          "AccountPublic"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccount"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/connections": {
      "get": {
        "tags": [
          "AccountPublic"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PublicAccountConnectionResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PublicAccountConnectionResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PublicAccountConnectionResponse"
                  }
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/picture": {
      "get": {
        "tags": [
          "AccountPublic"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "302": {
            "description": "Found"
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/background": {
      "get": {
        "tags": [
          "AccountPublic"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "302": {
            "description": "Found"
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/badges": {
      "get": {
        "tags": [
          "AccountPublic"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBadge"
                  }
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/board": {
      "get": {
        "tags": [
          "AccountPublic"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountBoardItem"
                  }
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/{name}/credits": {
      "get": {
        "tags": [
          "AccountPublic"
        ],
        "parameters": [
          {
            "name": "name",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "number",
                  "format": "double"
                }
              },
              "application/json": {
                "schema": {
                  "type": "number",
                  "format": "double"
                }
              },
              "text/json": {
                "schema": {
                  "type": "number",
                  "format": "double"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/search": {
      "get": {
        "tags": [
          "AccountPublic"
        ],
        "parameters": [
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/rewind/{code}": {
      "get": {
        "tags": [
          "AccountRewind"
        ],
        "parameters": [
          {
            "name": "code",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              }
            }
          }
        }
      }
    },
    "/passport/rewind/me": {
      "get": {
        "tags": [
          "AccountRewind"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              }
            }
          }
        }
      }
    },
    "/passport/rewind/me/{year}/public": {
      "post": {
        "tags": [
          "AccountRewind"
        ],
        "parameters": [
          {
            "name": "year",
            "in": "path",
            "required": true,
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              }
            }
          }
        }
      }
    },
    "/passport/rewind/me/{year}/private": {
      "post": {
        "tags": [
          "AccountRewind"
        ],
        "parameters": [
          {
            "name": "year",
            "in": "path",
            "required": true,
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRewindPoint"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/stats": {
      "get": {
        "tags": [
          "AccountStatsAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AccountStatsResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountStatsResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AccountStatsResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/affiliations": {
      "post": {
        "tags": [
          "AffiliationSpell"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAffiliationSpellRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAffiliationSpellRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAffiliationSpellRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationSpell"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationSpell"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationSpell"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "AffiliationSpell"
        ],
        "parameters": [
          {
            "name": "order",
            "in": "query",
            "schema": {
              "type": "string",
              "default": "date"
            }
          },
          {
            "name": "desc",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAffiliationSpell"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAffiliationSpell"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAffiliationSpell"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/affiliations/{spell}/results": {
      "post": {
        "tags": [
          "AffiliationSpell"
        ],
        "parameters": [
          {
            "name": "spell",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAffiliationResultRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAffiliationResultRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAffiliationResultRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationResult"
                }
              }
            }
          }
        }
      }
    },
    "/passport/affiliations/{id}": {
      "get": {
        "tags": [
          "AffiliationSpell"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationSpell"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationSpell"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAffiliationSpell"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "AffiliationSpell"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/affiliations/{id}/results": {
      "get": {
        "tags": [
          "AffiliationSpell"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "desc",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAffiliationResult"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAffiliationResult"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAffiliationResult"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/.well-known/badges": {
      "get": {
        "tags": [
          "BadgesDiscovery"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/.well-known/badges/icons/{iconName}": {
      "get": {
        "tags": [
          "BadgesDiscovery"
        ],
        "parameters": [
          {
            "name": "iconName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/domain-blocks": {
      "get": {
        "tags": [
          "DomainTrust"
        ],
        "parameters": [
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "limit",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnDomainBlock"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnDomainBlock"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnDomainBlock"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "DomainTrust"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateBlockRuleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateBlockRuleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateBlockRuleRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              }
            }
          }
        }
      }
    },
    "/passport/domain-blocks/{id}": {
      "get": {
        "tags": [
          "DomainTrust"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "DomainTrust"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateBlockRuleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateBlockRuleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateBlockRuleRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDomainBlock"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "DomainTrust"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/domain-blocks/validate": {
      "post": {
        "tags": [
          "DomainTrust"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ValidateUrlRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ValidateUrlRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ValidateUrlRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/DomainValidationResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/DomainValidationResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/DomainValidationResult"
                }
              }
            }
          }
        }
      }
    },
    "/passport/domain-blocks/check": {
      "get": {
        "tags": [
          "DomainTrust"
        ],
        "parameters": [
          {
            "name": "url",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/domain-blocks/metrics": {
      "get": {
        "tags": [
          "DomainTrust"
        ],
        "parameters": [
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "limit",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DomainValidationMetricResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DomainValidationMetricResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/DomainValidationMetricResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/domain-blocks/metrics/{domain}": {
      "get": {
        "tags": [
          "DomainTrust"
        ],
        "parameters": [
          {
            "name": "domain",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/DomainValidationMetricResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/DomainValidationMetricResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/DomainValidationMetricResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/fortune": {
      "get": {
        "tags": [
          "FortuneSaying"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FortuneSaying"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FortuneSaying"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FortuneSaying"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/fortune/random": {
      "get": {
        "tags": [
          "FortuneSaying"
        ],
        "parameters": [
          {
            "name": "language",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FortuneSaying"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FortuneSaying"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FortuneSaying"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/friends/overview": {
      "get": {
        "tags": [
          "Friends"
        ],
        "parameters": [
          {
            "name": "includeOffline",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FriendOverviewItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FriendOverviewItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FriendOverviewItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/ip-check/geo": {
      "get": {
        "tags": [
          "IpCheck"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/GeoIpResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/GeoIpResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/GeoIpResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/ip-check": {
      "get": {
        "tags": [
          "IpCheck"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/IpCheckResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/IpCheckResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/IpCheckResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/pins": {
      "post": {
        "tags": [
          "LocationPin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePinRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePinRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreatePinRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              }
            }
          }
        },
        "deprecated": true
      }
    },
    "/passport/pins/{id}/location": {
      "put": {
        "tags": [
          "LocationPin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateLocationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateLocationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateLocationRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              }
            }
          }
        },
        "deprecated": true
      }
    },
    "/passport/pins/{id}": {
      "delete": {
        "tags": [
          "LocationPin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        },
        "deprecated": true
      },
      "get": {
        "tags": [
          "LocationPin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "locationWkt",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              }
            }
          }
        },
        "deprecated": true
      }
    },
    "/passport/pins/nearby": {
      "get": {
        "tags": [
          "LocationPin"
        ],
        "parameters": [
          {
            "name": "locationWkt",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "visibility",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/LocationVisibility"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              }
            }
          }
        },
        "deprecated": true
      }
    },
    "/passport/pins/{id}/disconnect": {
      "post": {
        "tags": [
          "LocationPin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/DisconnectRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/DisconnectRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/DisconnectRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        },
        "deprecated": true
      }
    },
    "/passport/pins/{id}/stream": {
      "get": {
        "tags": [
          "LocationPin"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        },
        "deprecated": true
      }
    },
    "/passport/pins/me": {
      "get": {
        "tags": [
          "LocationPin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              }
            }
          }
        },
        "deprecated": true
      }
    },
    "/passport/spells/activation/resend": {
      "post": {
        "tags": [
          "MagicSpell"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/spells/{spellId}/resend": {
      "post": {
        "tags": [
          "MagicSpell"
        ],
        "parameters": [
          {
            "name": "spellId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/spells/{spellWord}": {
      "get": {
        "tags": [
          "MagicSpell"
        ],
        "parameters": [
          {
            "name": "spellWord",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/spells/{spellWord}/apply": {
      "post": {
        "tags": [
          "MagicSpell"
        ],
        "parameters": [
          {
            "name": "spellWord",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/MagicSpellApplyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/MagicSpellApplyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/MagicSpellApplyRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/meets": {
      "post": {
        "tags": [
          "Meet"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateMeetRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateMeetRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateMeetRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "status",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/MeetStatus"
            }
          },
          {
            "name": "hostOnly",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMeet"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMeet"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMeet"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/meets/nearby": {
      "get": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "locationWkt",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "distanceMeters",
            "in": "query",
            "schema": {
              "type": "number",
              "format": "double",
              "default": 1000
            }
          },
          {
            "name": "status",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/MeetStatus"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMeet"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMeet"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnMeet"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/meets/{id}": {
      "get": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "locationWkt",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/meets/{id}/complete": {
      "post": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              }
            }
          }
        }
      }
    },
    "/passport/meets/{id}/visibility": {
      "patch": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateVisibilityRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateVisibilityRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateVisibilityRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnMeet"
                }
              }
            }
          }
        }
      }
    },
    "/passport/meets/{id}/join": {
      "post": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/meets/{id}/pin": {
      "post": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateOrUpdatePinRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateOrUpdatePinRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateOrUpdatePinRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnLocationPin"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/meets/{id}/pins": {
      "get": {
        "tags": [
          "Meet"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLocationPin"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/nearby/presence-tokens": {
      "post": {
        "tags": [
          "Nearby"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PresenceTokensRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PresenceTokensRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PresenceTokensRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PresenceTokensResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PresenceTokensResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PresenceTokensResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/nearby/resolve": {
      "post": {
        "tags": [
          "Nearby"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ResolveRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ResolveRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ResolveRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ResolveResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ResolveResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ResolveResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/nfc": {
      "get": {
        "tags": [
          "Nfc"
        ],
        "parameters": [
          {
            "name": "uid",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "picc_data",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "e",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "cmac",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/nfc/lookup": {
      "get": {
        "tags": [
          "Nfc"
        ],
        "parameters": [
          {
            "name": "uid",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/nfc/tags/{id}": {
      "get": {
        "tags": [
          "Nfc"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcResolveResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/nfc/tags": {
      "get": {
        "tags": [
          "Nfc"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/NfcTagResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/NfcTagResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/NfcTagResponse"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Nfc"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RegisterTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RegisterTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RegisterTagRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/nfc/tags/claim": {
      "post": {
        "tags": [
          "Nfc"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ClaimTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ClaimTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ClaimTagRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/nfc/tags/{tagId}": {
      "patch": {
        "tags": [
          "Nfc"
        ],
        "parameters": [
          {
            "name": "tagId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateTagRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Nfc"
        ],
        "parameters": [
          {
            "name": "tagId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/nfc/tags/{tagId}/lock": {
      "post": {
        "tags": [
          "Nfc"
        ],
        "parameters": [
          {
            "name": "tagId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NfcTagResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/nfc/tags": {
      "post": {
        "tags": [
          "NfcAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateEncryptedTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateEncryptedTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateEncryptedTagRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/EncryptedTagDto"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/EncryptedTagDto"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/EncryptedTagDto"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "NfcAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EncryptedTagDto"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EncryptedTagDto"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EncryptedTagDto"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/notable-days": {
      "get": {
        "tags": [
          "NotableDays"
        ],
        "parameters": [
          {
            "name": "year",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "region",
            "in": "query",
            "schema": {
              "type": "string",
              "default": "CN"
            }
          },
          {
            "name": "tag",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotableDay"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotableDay"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotableDay"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "NotableDays"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/NotableDayRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/NotableDayRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/NotableDayRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              }
            }
          }
        }
      }
    },
    "/passport/notable-days/{id}": {
      "get": {
        "tags": [
          "NotableDays"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "NotableDays"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/NotableDayRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/NotableDayRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/NotableDayRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotableDay"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "NotableDays"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/passkit/v1": {
      "get": {
        "tags": [
          "PassKit"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/passkit/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}/{serialNumber}": {
      "post": {
        "tags": [
          "PassKit"
        ],
        "parameters": [
          {
            "name": "deviceLibraryIdentifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "passTypeIdentifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "serialNumber",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PassRegistrationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PassRegistrationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PassRegistrationRequest"
              }
            }
          }
        },
        "responses": {
          "201": {
            "description": "Created"
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PassKit"
        ],
        "parameters": [
          {
            "name": "deviceLibraryIdentifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "passTypeIdentifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "serialNumber",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      }
    },
    "/passport/passkit/v1/devices/{deviceLibraryIdentifier}/registrations/{passTypeIdentifier}": {
      "get": {
        "tags": [
          "PassKit"
        ],
        "parameters": [
          {
            "name": "deviceLibraryIdentifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "passTypeIdentifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "passesUpdatedSince",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PassSerialNumbersResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PassSerialNumbersResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PassSerialNumbersResponse"
                }
              }
            }
          },
          "204": {
            "description": "No Content"
          }
        }
      }
    },
    "/passport/passkit/v1/passes/{passTypeIdentifier}/{serialNumber}": {
      "get": {
        "tags": [
          "PassKit"
        ],
        "parameters": [
          {
            "name": "passTypeIdentifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "serialNumber",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/passkit/v1/log": {
      "post": {
        "tags": [
          "PassKit"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PassLogRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PassLogRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PassLogRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/activities": {
      "get": {
        "tags": [
          "PresenceActivity"
        ],
        "parameters": [
          {
            "name": "includeExpired",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/PresenceType"
            }
          },
          {
            "name": "provider",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "referenceId",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "term",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPresenceActivity"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPresenceActivity"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPresenceActivity"
                  }
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PresenceActivity"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SetActivityRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetActivityRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetActivityRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPresenceActivity"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPresenceActivity"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPresenceActivity"
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PresenceActivity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "manualId",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "204": {
            "description": "No Content"
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/activities/{identifier}": {
      "get": {
        "tags": [
          "PresenceActivity"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPresenceActivity"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPresenceActivity"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPresenceActivity"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/activities/{id}": {
      "put": {
        "tags": [
          "PresenceActivity"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SetActivityRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetActivityRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetActivityRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPresenceActivity"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPresenceActivity"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPresenceActivity"
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/presence/artworks": {
      "post": {
        "tags": [
          "PresenceArtwork"
        ],
        "requestBody": {
          "content": {
            "multipart/form-data": {
              "schema": {
                "required": [
                  "File"
                ],
                "type": "object",
                "properties": {
                  "File": {
                    "type": "string",
                    "format": "binary"
                  }
                }
              },
              "encoding": {
                "File": {
                  "style": "form"
                }
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PresenceArtworkResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PresenceArtworkResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PresenceArtworkResponse"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/presence/artworks/{hash}": {
      "get": {
        "tags": [
          "PresenceArtwork"
        ],
        "parameters": [
          {
            "name": "hash",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/progression/achievements": {
      "get": {
        "tags": [
          "Progression"
        ],
        "parameters": [
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/ProgressionAchievementState"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/ProgressionAchievementState"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/ProgressionAchievementState"
                  }
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/progression/achievements/stats": {
      "get": {
        "tags": [
          "Progression"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProgressionAchievementStats"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProgressionAchievementStats"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProgressionAchievementStats"
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/progression/quests": {
      "get": {
        "tags": [
          "Progression"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/ProgressionQuestState"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/ProgressionQuestState"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/ProgressionQuestState"
                  }
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      }
    },
    "/passport/accounts/me/progression/grants": {
      "get": {
        "tags": [
          "Progression"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnProgressRewardGrant"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnProgressRewardGrant"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnProgressRewardGrant"
                  }
                }
              }
            }
          },
          "401": {
            "description": "Unauthorized",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ApiError"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/progression/achievements": {
      "get": {
        "tags": [
          "ProgressionAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAchievementDefinition"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAchievementDefinition"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAchievementDefinition"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "ProgressionAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ProgressionDefinitionUpsertRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ProgressionDefinitionUpsertRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ProgressionDefinitionUpsertRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/progression/achievements/{identifier}": {
      "put": {
        "tags": [
          "ProgressionAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ProgressionDefinitionUpsertRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ProgressionDefinitionUpsertRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ProgressionDefinitionUpsertRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/progression/achievements/{identifier}/enable": {
      "post": {
        "tags": [
          "ProgressionAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "enabled",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": true
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAchievementDefinition"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/progression/quests": {
      "get": {
        "tags": [
          "ProgressionAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnQuestDefinition"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnQuestDefinition"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnQuestDefinition"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "ProgressionAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/QuestDefinitionUpsertRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/QuestDefinitionUpsertRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/QuestDefinitionUpsertRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/progression/quests/{identifier}": {
      "put": {
        "tags": [
          "ProgressionAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/QuestDefinitionUpsertRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/QuestDefinitionUpsertRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/QuestDefinitionUpsertRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/progression/quests/{identifier}/enable": {
      "post": {
        "tags": [
          "ProgressionAdmin"
        ],
        "parameters": [
          {
            "name": "identifier",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "enabled",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": true
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnQuestDefinition"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/progression/sync": {
      "post": {
        "tags": [
          "ProgressionAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/progression/test-ws-packet": {
      "post": {
        "tags": [
          "ProgressionAdmin"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "kind",
            "in": "query",
            "schema": {
              "type": "string",
              "default": "achievement"
            }
          },
          {
            "name": "identifier",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "title",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/realms/quota": {
      "get": {
        "tags": [
          "Realm"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/RealmQuotaRecordResourceQuotaResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/RealmQuotaRecordResourceQuotaResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/RealmQuotaRecordResourceQuotaResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/realms": {
      "get": {
        "tags": [
          "Realm"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Realm"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/invites": {
      "get": {
        "tags": [
          "Realm"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/invites/{slug}": {
      "post": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmMemberRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmMemberRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmMemberRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/invites/{slug}/accept": {
      "post": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/invites/{slug}/decline": {
      "post": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/realms/{slug}/members": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "withStatus",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "accountName",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "labelId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/members/me": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/members/me/profile": {
      "patch": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmMemberProfileRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmMemberProfileRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmMemberProfileRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/labels": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmLabel"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmLabel"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmLabel"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmLabel"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmLabel"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmLabel"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/labels/{labelId}": {
      "patch": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "labelId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmLabel"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmLabel"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmLabel"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "labelId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/realms/{slug}/members/{memberId}/label": {
      "patch": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelAssignmentRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelAssignmentRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmLabelAssignmentRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/boosts": {
      "post": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmBoostRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmBoostRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmBoostRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/RealmBoostResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/RealmBoostResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/RealmBoostResponse"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/realms/{slug}/boosts/leaderboard": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/realms/{slug}/members/{memberId}/experience": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmExperienceRecord"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmExperienceRecord"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmExperienceRecord"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/members/{memberId}": {
      "delete": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/realms/{slug}/members/{memberId}/role": {
      "patch": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "integer",
                "format": "int32"
              }
            },
            "text/json": {
              "schema": {
                "type": "integer",
                "format": "int32"
              }
            },
            "application/*+json": {
              "schema": {
                "type": "integer",
                "format": "int32"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/permissions/roles": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmRolePermission"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmRolePermission"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmRolePermission"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRolePermissionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRolePermissionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmRolePermissionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmRolePermission"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmRolePermission"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmRolePermission"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/permissions/users/{accountId}": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmUserPermission"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmUserPermission"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmUserPermission"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/permissions/users": {
      "post": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmUserPermissionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RealmUserPermissionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RealmUserPermissionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmUserPermission"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmUserPermission"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmUserPermission"
                }
              }
            }
          }
        }
      }
    },
    "/passport/realms/{slug}/posts/moderation-logs": {
      "get": {
        "tags": [
          "Realm"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/realms": {
      "get": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "isPublic",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "isCommunity",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "verified",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "accountId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/realms/{slug}": {
      "get": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/AdminRealmDetail"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminRealmDetail"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminRealmDetail"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateRealmRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateRealmRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateRealmRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/admin/realms/{slug}/verification": {
      "post": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SetRealmVerificationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetRealmVerificationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetRealmVerificationRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealm"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/realms/{slug}/members": {
      "get": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "role",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "pendingOnly",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealmMember"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/realms/{slug}/members/{memberId}/role": {
      "patch": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateMemberRoleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateMemberRoleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateMemberRoleRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnRealmMember"
                }
              }
            }
          }
        }
      }
    },
    "/passport/admin/realms/{slug}/members/{memberId}": {
      "delete": {
        "tags": [
          "RealmAdmin"
        ],
        "parameters": [
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "memberId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/realms/public": {
      "get": {
        "tags": [
          "RealmPublic"
        ],
        "parameters": [
          {
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnRealm"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships": {
      "get": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountRelationship"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountRelationship"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountRelationship"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/requests": {
      "get": {
        "tags": [
          "Relationship"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountRelationship"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountRelationship"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccountRelationship"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/{accountId}": {
      "post": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/{accountId}/friends": {
      "post": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/passport/relationships/{accountId}/friends/accept": {
      "post": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/{accountId}/friends/decline": {
      "post": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/{accountId}/block": {
      "post": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipActionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipActionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipActionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/{accountId}/mute": {
      "post": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipActionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipActionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RelationshipActionRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/{accountId}/close-friend": {
      "post": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/close-friends": {
      "get": {
        "tags": [
          "Relationship"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/inspect/{accountId}": {
      "get": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/InspectRelationshipResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/InspectRelationshipResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/InspectRelationshipResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/{accountId}/alias": {
      "patch": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AliasRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AliasRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AliasRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAccountRelationship"
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/{accountId}/mutual-friends": {
      "get": {
        "tags": [
          "Relationship"
        ],
        "parameters": [
          {
            "name": "accountId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAccount"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/relationships/sync": {
      "post": {
        "tags": [
          "Relationship"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SyncRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/RelationshipSyncResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/RelationshipSyncResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/RelationshipSyncResponse"
                }
              }
            }
          }
        }
      }
    },
    "/passport/tickets": {
      "post": {
        "tags": [
          "Ticket"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateTicketRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateTicketRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateTicketRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              }
            }
          },
          "400": {
            "description": "Bad Request",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "creatorId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "assigneeId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/TicketType"
            }
          },
          {
            "name": "status",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/TicketStatus"
            }
          },
          {
            "name": "priority",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/TicketPriority"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnTicket"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnTicket"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnTicket"
                  }
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/tickets/me": {
      "get": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "status",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/TicketStatus"
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnTicket"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnTicket"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnTicket"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/passport/tickets/{id}": {
      "get": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateTicketRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateTicketRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateTicketRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "204": {
            "description": "No Content"
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/tickets/{id}/messages": {
      "post": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AddMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AddMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AddMessageRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicketMessage"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicketMessage"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicketMessage"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/tickets/{id}/status": {
      "post": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateStatusRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateStatusRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateStatusRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/tickets/{id}/assign": {
      "post": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AssignRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AssignRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AssignRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTicket"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/passport/tickets/count": {
      "get": {
        "tags": [
          "Ticket"
        ],
        "parameters": [
          {
            "name": "creatorId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "assigneeId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "status",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/TicketStatus"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": { }
              },
              "application/json": {
                "schema": { }
              },
              "text/json": {
                "schema": { }
              }
            }
          },
          "403": {
            "description": "Forbidden",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "AccountAuthFactorSummary": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "trustworthy": {
            "type": "integer",
            "format": "int32"
          },
          "has_secret": {
            "type": "boolean"
          },
          "config": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "enabled_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "AccountContactType": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "AccountStatsResponse": {
        "type": "object",
        "properties": {
          "calculated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "total_profiled_accounts": {
            "type": "integer",
            "format": "int64"
          },
          "active_users_last_day": {
            "type": "integer",
            "format": "int64"
          },
          "active_users_last_week": {
            "type": "integer",
            "format": "int64"
          },
          "active_users_last_month": {
            "type": "integer",
            "format": "int64"
          },
          "registered_users_last_day": {
            "type": "integer",
            "format": "int64"
          },
          "registered_users_last_week": {
            "type": "integer",
            "format": "int64"
          },
          "registered_users_last_month": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "AccountTimelineItem": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "event_type": {
            "$ref": "#/components/schemas/TimelineEventType"
          },
          "status": {
            "$ref": "#/components/schemas/SnAccountStatus"
          },
          "activity": {
            "$ref": "#/components/schemas/SnPresenceActivity"
          }
        },
        "additionalProperties": false
      },
      "AddMessageRequest": {
        "required": [
          "content"
        ],
        "type": "object",
        "properties": {
          "content": {
            "maxLength": 16384,
            "minLength": 1,
            "type": "string"
          },
          "file_ids": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminAccountActivityMetricsResponse": {
        "type": "object",
        "properties": {
          "calculated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "current_day_started_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "daily_active_users": {
            "type": "integer",
            "format": "int32"
          },
          "weekly_active_users": {
            "type": "integer",
            "format": "int32"
          },
          "monthly_active_users": {
            "type": "integer",
            "format": "int32"
          },
          "previous_daily_active_users": {
            "type": "integer",
            "format": "int32"
          },
          "previous_weekly_active_users": {
            "type": "integer",
            "format": "int32"
          },
          "previous_monthly_active_users": {
            "type": "integer",
            "format": "int32"
          },
          "new_accounts_today": {
            "type": "integer",
            "format": "int32"
          },
          "new_accounts_this_week": {
            "type": "integer",
            "format": "int32"
          },
          "new_accounts_this_month": {
            "type": "integer",
            "format": "int32"
          },
          "total_profiled_accounts": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "AdminAccountDetailResponse": {
        "type": "object",
        "properties": {
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "status": {
            "$ref": "#/components/schemas/SnAccountStatus"
          },
          "activities": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPresenceActivity"
            },
            "nullable": true
          },
          "contacts": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountContact"
            },
            "nullable": true
          },
          "auth_factors": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/AccountAuthFactorSummary"
            },
            "nullable": true
          },
          "badges": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountBadge"
            },
            "nullable": true
          },
          "badge_count": {
            "type": "integer",
            "format": "int32"
          },
          "board": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountBoardItem"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminAccountSummaryResponse": {
        "type": "object",
        "properties": {
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "status": {
            "$ref": "#/components/schemas/SnAccountStatus"
          },
          "badge_count": {
            "type": "integer",
            "format": "int32"
          },
          "active_activity_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "AdminBadgeRequest": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "label": {
            "type": "string",
            "nullable": true
          },
          "caption": {
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "AdminBoardItemRequest": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "kind": {
            "$ref": "#/components/schemas/SnAccountBoardItemKind"
          },
          "widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "custom_app_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "custom_app_widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "is_enabled": {
            "type": "boolean"
          },
          "payload": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminPushBoardPayloadRequest": {
        "type": "object",
        "properties": {
          "payload": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminRealmDetail": {
        "type": "object",
        "properties": {
          "realm": {
            "$ref": "#/components/schemas/SnRealm"
          },
          "member_count": {
            "type": "integer",
            "format": "int32"
          },
          "pending_invite_count": {
            "type": "integer",
            "format": "int32"
          },
          "label_count": {
            "type": "integer",
            "format": "int32"
          },
          "active_boost_contribution_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "AdminUpdateRealmRequest": {
        "type": "object",
        "properties": {
          "slug": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "is_community": {
            "type": "boolean",
            "nullable": true
          },
          "is_public": {
            "type": "boolean",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AffiliationSpellType": {
        "enum": [
          0
        ],
        "type": "integer",
        "format": "int32"
      },
      "AliasRequest": {
        "type": "object",
        "properties": {
          "alias": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ApiError": {
        "type": "object",
        "properties": {
          "code": {
            "type": "string",
            "nullable": true
          },
          "message": {
            "type": "string",
            "nullable": true
          },
          "status": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "detail": {
            "type": "string",
            "nullable": true
          },
          "traceId": {
            "type": "string",
            "nullable": true
          },
          "errors": {
            "type": "object",
            "additionalProperties": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AssignRequest": {
        "type": "object",
        "properties": {
          "assignee_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BoardItemRequest": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "kind": {
            "$ref": "#/components/schemas/SnAccountBoardItemKind"
          },
          "widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "custom_app_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "custom_app_widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "is_enabled": {
            "type": "boolean"
          },
          "payload": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CalendarEventType": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "CalendarSearchResultItem": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/CalendarEventType"
          },
          "start_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "user_event": {
            "$ref": "#/components/schemas/SnUserCalendarEvent"
          },
          "notable_day": {
            "$ref": "#/components/schemas/NotableDay"
          }
        },
        "additionalProperties": false
      },
      "CheckInFortuneReport": {
        "type": "object",
        "properties": {
          "version": {
            "type": "integer",
            "format": "int32"
          },
          "poem": {
            "type": "string",
            "nullable": true
          },
          "summary": {
            "type": "string",
            "nullable": true
          },
          "summary_detail": {
            "type": "string",
            "nullable": true
          },
          "wish": {
            "type": "string",
            "nullable": true
          },
          "love": {
            "type": "string",
            "nullable": true
          },
          "study": {
            "type": "string",
            "nullable": true
          },
          "career": {
            "type": "string",
            "nullable": true
          },
          "health": {
            "type": "string",
            "nullable": true
          },
          "lost_item": {
            "type": "string",
            "nullable": true
          },
          "lucky_color": {
            "type": "string",
            "nullable": true
          },
          "lucky_direction": {
            "type": "string",
            "nullable": true
          },
          "lucky_time": {
            "type": "string",
            "nullable": true
          },
          "lucky_item": {
            "type": "string",
            "nullable": true
          },
          "lucky_action": {
            "type": "string",
            "nullable": true
          },
          "avoid_action": {
            "type": "string",
            "nullable": true
          },
          "ritual": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CheckInFortuneTip": {
        "type": "object",
        "properties": {
          "is_positive": {
            "type": "boolean"
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "content": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CheckInResultLevel": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5
        ],
        "type": "integer",
        "format": "int32"
      },
      "ClaimTagRequest": {
        "type": "object",
        "properties": {
          "uid": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "record_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ContentSensitiveMark": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6,
          7,
          8,
          9,
          10,
          11,
          12
        ],
        "type": "integer",
        "format": "int32"
      },
      "CreateAdminMagicSpellRequest": {
        "required": [
          "type"
        ],
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/MagicSpellType"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "affected_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "code": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "prevent_repeat": {
            "type": "boolean"
          },
          "send_email": {
            "type": "boolean"
          },
          "bypass_verify": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "CreateAffiliationResultRequest": {
        "type": "object",
        "properties": {
          "resource_identifier": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateAffiliationSpellRequest": {
        "type": "object",
        "properties": {
          "spell": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateBlockRuleRequest": {
        "required": [
          "domain_pattern"
        ],
        "type": "object",
        "properties": {
          "domain_pattern": {
            "maxLength": 512,
            "minLength": 1,
            "type": "string"
          },
          "protocol": {
            "maxLength": 16,
            "type": "string",
            "nullable": true
          },
          "port_restriction": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "reason": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          },
          "trust_level": {
            "$ref": "#/components/schemas/DomainTrustLevel"
          },
          "is_active": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "CreateCalendarEventRequest": {
        "type": "object",
        "properties": {
          "title": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "location": {
            "type": "string",
            "nullable": true
          },
          "start_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_all_day": {
            "type": "boolean"
          },
          "visibility": {
            "$ref": "#/components/schemas/EventVisibility"
          },
          "recurrence": {
            "$ref": "#/components/schemas/RecurrencePattern"
          },
          "tags": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "icon_id": {
            "type": "string",
            "nullable": true
          },
          "background_id": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateEncryptedTagRequest": {
        "required": [
          "sun_key",
          "uid"
        ],
        "type": "object",
        "properties": {
          "uid": {
            "maxLength": 64,
            "minLength": 1,
            "type": "string"
          },
          "sun_key": {
            "minLength": 1,
            "type": "string"
          },
          "assigned_user_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateMeetRequest": {
        "type": "object",
        "properties": {
          "visibility": {
            "$ref": "#/components/schemas/LocationVisibility"
          },
          "notes": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "image_id": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "location_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "location_address": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location_wkt": {
            "type": "string",
            "nullable": true
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "expires_in_seconds": {
            "maximum": 86400,
            "minimum": 60,
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateOrUpdatePinRequest": {
        "type": "object",
        "properties": {
          "visibility": {
            "$ref": "#/components/schemas/LocationVisibility"
          },
          "location_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "location_address": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location_wkt": {
            "type": "string",
            "nullable": true
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "keep_on_disconnect": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "CreatePinRequest": {
        "type": "object",
        "properties": {
          "visibility": {
            "$ref": "#/components/schemas/LocationVisibility"
          },
          "location_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "location_address": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location_wkt": {
            "type": "string",
            "nullable": true
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "keep_on_disconnect": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "CreateTicketRequest": {
        "required": [
          "title",
          "type"
        ],
        "type": "object",
        "properties": {
          "title": {
            "maxLength": 256,
            "minLength": 3,
            "type": "string"
          },
          "content": {
            "maxLength": 16384,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/TicketType"
          },
          "priority": {
            "$ref": "#/components/schemas/TicketPriority"
          },
          "file_ids": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "resources": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "DailyEventResponse": {
        "type": "object",
        "properties": {
          "date": {
            "$ref": "#/components/schemas/Instant"
          },
          "check_in_result": {
            "$ref": "#/components/schemas/SnCheckInResult"
          },
          "statuses": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountStatus"
            },
            "nullable": true
          },
          "user_events": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/UserCalendarEventDto"
            },
            "nullable": true
          },
          "notable_days": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/NotableDay"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "DisconnectRequest": {
        "type": "object",
        "properties": {
          "keep_on_disconnect": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "DomainTrustLevel": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "DomainValidationMetricResponse": {
        "type": "object",
        "properties": {
          "domain": {
            "type": "string",
            "nullable": true
          },
          "check_count": {
            "type": "integer",
            "format": "int32"
          },
          "blocked_count": {
            "type": "integer",
            "format": "int32"
          },
          "verified_count": {
            "type": "integer",
            "format": "int32"
          },
          "last_checked_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "DomainValidationResult": {
        "type": "object",
        "properties": {
          "is_allowed": {
            "type": "boolean"
          },
          "is_verified": {
            "type": "boolean"
          },
          "block_reason": {
            "type": "string",
            "nullable": true
          },
          "matched_rule": {
            "$ref": "#/components/schemas/SnDomainBlock"
          },
          "matched_source": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "EncryptedTagDto": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "uid": {
            "type": "string",
            "nullable": true
          },
          "user_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "is_active": {
            "type": "boolean"
          },
          "is_locked": {
            "type": "boolean"
          },
          "last_seen_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "EventCountdownItem": {
        "type": "object",
        "properties": {
          "event_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "event_type": {
            "$ref": "#/components/schemas/CalendarEventType"
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "location": {
            "type": "string",
            "nullable": true
          },
          "start_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_all_day": {
            "type": "boolean"
          },
          "days_remaining": {
            "type": "integer",
            "format": "int32"
          },
          "hours_remaining": {
            "type": "integer",
            "format": "int32"
          },
          "is_ongoing": {
            "type": "boolean"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "icon": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          }
        },
        "additionalProperties": false
      },
      "EventVisibility": {
        "enum": [
          0,
          100,
          200
        ],
        "type": "integer",
        "format": "int32"
      },
      "FortuneSaying": {
        "type": "object",
        "properties": {
          "content": {
            "type": "string",
            "nullable": true
          },
          "source": {
            "type": "string",
            "nullable": true
          },
          "language": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "FriendOverviewItem": {
        "type": "object",
        "properties": {
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "status": {
            "$ref": "#/components/schemas/SnAccountStatus"
          },
          "activities": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPresenceActivity"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "GeoIpResponse": {
        "type": "object",
        "properties": {
          "city": {
            "type": "string",
            "nullable": true
          },
          "country": {
            "type": "string",
            "nullable": true
          },
          "country_code": {
            "type": "string",
            "nullable": true
          },
          "subdivision": {
            "type": "string",
            "nullable": true
          },
          "subdivision_code": {
            "type": "string",
            "nullable": true
          },
          "continent_code": {
            "type": "string",
            "nullable": true
          },
          "time_zone": {
            "type": "string",
            "nullable": true
          },
          "latitude": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "longitude": {
            "type": "number",
            "format": "double",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "GeoPoint": {
        "type": "object",
        "properties": {
          "latitude": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "longitude": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "country_code": {
            "type": "string",
            "nullable": true
          },
          "country": {
            "type": "string",
            "nullable": true
          },
          "city": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "InspectRelationshipResponse": {
        "type": "object",
        "properties": {
          "friends": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccount"
            },
            "nullable": true
          },
          "blocked": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccount"
            },
            "nullable": true
          },
          "muted": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccount"
            },
            "nullable": true
          },
          "pending": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccount"
            },
            "nullable": true
          },
          "close_friends": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccount"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "Instant": {
        "type": "object",
        "additionalProperties": false
      },
      "IpCheckResponse": {
        "type": "object",
        "properties": {
          "client_ip": {
            "type": "string",
            "nullable": true
          },
          "remote_ip": {
            "type": "string",
            "nullable": true
          },
          "x_forwarded_for": {
            "type": "string",
            "nullable": true
          },
          "x_forwarded_proto": {
            "type": "string",
            "nullable": true
          },
          "x_forwarded_host": {
            "type": "string",
            "nullable": true
          },
          "x_real_ip": {
            "type": "string",
            "nullable": true
          },
          "cf_connecting_ip": {
            "type": "string",
            "nullable": true
          },
          "geo": {
            "$ref": "#/components/schemas/GeoIpResponse"
          },
          "headers": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "IsoDayOfWeek": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6,
          7
        ],
        "type": "integer",
        "format": "int32"
      },
      "LocationPinStatus": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "LocationVisibility": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "MagicSpellApplyRequest": {
        "type": "object",
        "properties": {
          "new_password": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "MagicSpellType": {
        "enum": [
          0,
          1,
          2,
          3,
          4
        ],
        "type": "integer",
        "format": "int32"
      },
      "MeetStatus": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "MergedCalendarEvent": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/CalendarEventType"
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "location": {
            "type": "string",
            "nullable": true
          },
          "start_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_all_day": {
            "type": "boolean"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "MergedDailyEventResponse": {
        "type": "object",
        "properties": {
          "date": {
            "$ref": "#/components/schemas/Instant"
          },
          "check_in_result": {
            "$ref": "#/components/schemas/SnCheckInResult"
          },
          "statuses": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountStatus"
            },
            "nullable": true
          },
          "user_events": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/UserCalendarEventDto"
            },
            "nullable": true
          },
          "notable_days": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/NotableDay"
            },
            "nullable": true
          },
          "merged_events": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/MergedCalendarEvent"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "NfcResolveResponse": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "is_friend": {
            "type": "boolean"
          },
          "is_claimed": {
            "type": "boolean"
          },
          "actions": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "NfcTagResponse": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "uid": {
            "type": "string",
            "nullable": true
          },
          "label": {
            "type": "string",
            "nullable": true
          },
          "is_active": {
            "type": "boolean"
          },
          "is_locked": {
            "type": "boolean"
          },
          "is_encrypted": {
            "type": "boolean"
          },
          "sun_key": {
            "type": "string",
            "nullable": true
          },
          "user_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "last_seen_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "NotableDay": {
        "type": "object",
        "properties": {
          "date": {
            "$ref": "#/components/schemas/Instant"
          },
          "local_name": {
            "type": "string",
            "nullable": true
          },
          "global_name": {
            "type": "string",
            "nullable": true
          },
          "localizable_key": {
            "type": "string",
            "nullable": true
          },
          "country_code": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "occurrence_key": {
            "type": "string",
            "nullable": true
          },
          "holidays": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/NotableHolidayType"
            },
            "nullable": true
          },
          "tags": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/NotableDayTag"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "NotableDayRequest": {
        "required": [
          "end_date",
          "name",
          "start_date"
        ],
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "minLength": 1,
            "type": "string"
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "local_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "localizable_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "start_date": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_date": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_all_day": {
            "type": "boolean"
          },
          "region": {
            "maxLength": 8,
            "type": "string",
            "nullable": true
          },
          "tags": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/NotableDayTag"
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "is_recurring": {
            "type": "boolean"
          },
          "recurrence_pattern": {
            "maxLength": 16,
            "type": "string",
            "nullable": true
          },
          "is_period": {
            "type": "boolean"
          },
          "holiday_days": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "display_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "NotableDayTag": {
        "enum": [
          0,
          1,
          2,
          3,
          4
        ],
        "type": "integer",
        "format": "int32"
      },
      "NotableHolidayType": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5
        ],
        "type": "integer",
        "format": "int32"
      },
      "PassLogRequest": {
        "type": "object",
        "properties": {
          "logs": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PassRegistrationRequest": {
        "type": "object",
        "properties": {
          "push_token": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PassSerialNumbersResponse": {
        "type": "object",
        "properties": {
          "serial_numbers": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "last_updated": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PresenceArtworkResponse": {
        "type": "object",
        "properties": {
          "hash": {
            "type": "string",
            "nullable": true
          },
          "mime_type": {
            "type": "string",
            "nullable": true
          },
          "size": {
            "type": "integer",
            "format": "int64"
          },
          "url": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PresenceTokenDto": {
        "type": "object",
        "properties": {
          "slot": {
            "type": "integer",
            "format": "int64"
          },
          "token": {
            "type": "string",
            "nullable": true
          },
          "valid_from": {
            "$ref": "#/components/schemas/Instant"
          },
          "valid_to": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "PresenceTokensRequest": {
        "required": [
          "device_id"
        ],
        "type": "object",
        "properties": {
          "device_id": {
            "maxLength": 256,
            "minLength": 1,
            "type": "string"
          },
          "discoverable": {
            "type": "boolean"
          },
          "friend_only": {
            "type": "boolean"
          },
          "capabilities": {
            "type": "integer",
            "format": "int32"
          },
          "prefetch_slots": {
            "maximum": 30,
            "minimum": 1,
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "PresenceTokensResponse": {
        "type": "object",
        "properties": {
          "service_uuid": {
            "type": "string",
            "nullable": true
          },
          "slot_duration_sec": {
            "type": "integer",
            "format": "int32"
          },
          "tokens": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/PresenceTokenDto"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PresenceType": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "ProblemDetails": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "status": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "detail": {
            "type": "string",
            "nullable": true
          },
          "instance": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": { }
      },
      "ProfileRequest": {
        "type": "object",
        "properties": {
          "first_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "middle_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "last_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "gender": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "pronouns": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "time_zone": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "username_color": {
            "$ref": "#/components/schemas/UsernameColor"
          },
          "birthday": {
            "$ref": "#/components/schemas/Instant"
          },
          "links": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnProfileLink"
            },
            "nullable": true
          },
          "picture_id": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "background_id": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ProgressionAchievementState": {
        "type": "object",
        "properties": {
          "identifier": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "summary": {
            "type": "string",
            "nullable": true
          },
          "icon": {
            "type": "string",
            "nullable": true
          },
          "series_identifier": {
            "type": "string",
            "nullable": true
          },
          "series_title": {
            "type": "string",
            "nullable": true
          },
          "series_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "sort_order": {
            "type": "integer",
            "format": "int32"
          },
          "hidden": {
            "type": "boolean"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "is_progress_enabled": {
            "type": "boolean"
          },
          "is_currently_available": {
            "type": "boolean"
          },
          "available_from": {
            "$ref": "#/components/schemas/Instant"
          },
          "available_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "target_count": {
            "type": "integer",
            "format": "int32"
          },
          "progress_count": {
            "type": "integer",
            "format": "int32"
          },
          "current_streak": {
            "type": "integer",
            "format": "int32"
          },
          "best_streak": {
            "type": "integer",
            "format": "int32"
          },
          "series_total_steps": {
            "type": "integer",
            "format": "int32"
          },
          "series_completed_steps": {
            "type": "integer",
            "format": "int32"
          },
          "series_stages": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ProgressionSeriesStage"
            },
            "nullable": true
          },
          "is_completed": {
            "type": "boolean"
          },
          "completed_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "reward": {
            "$ref": "#/components/schemas/SnProgressRewardDefinition"
          }
        },
        "additionalProperties": false
      },
      "ProgressionAchievementStats": {
        "type": "object",
        "properties": {
          "total_count": {
            "type": "integer",
            "format": "int32"
          },
          "completed_count": {
            "type": "integer",
            "format": "int32"
          },
          "hidden_total_count": {
            "type": "integer",
            "format": "int32"
          },
          "hidden_completed_count": {
            "type": "integer",
            "format": "int32"
          },
          "completion_percentage": {
            "type": "number",
            "format": "double"
          }
        },
        "additionalProperties": false
      },
      "ProgressionDefinitionUpsertRequest": {
        "type": "object",
        "properties": {
          "identifier": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "summary": {
            "type": "string",
            "nullable": true
          },
          "icon": {
            "type": "string",
            "nullable": true
          },
          "series_identifier": {
            "type": "string",
            "nullable": true
          },
          "series_title": {
            "type": "string",
            "nullable": true
          },
          "series_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "sort_order": {
            "type": "integer",
            "format": "int32"
          },
          "hidden": {
            "type": "boolean"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "is_seed_managed": {
            "type": "boolean"
          },
          "is_progress_enabled": {
            "type": "boolean"
          },
          "available_from": {
            "$ref": "#/components/schemas/Instant"
          },
          "available_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "target_count": {
            "type": "integer",
            "format": "int32"
          },
          "trigger": {
            "$ref": "#/components/schemas/SnProgressTriggerDefinition"
          },
          "reward": {
            "$ref": "#/components/schemas/SnProgressRewardDefinition"
          }
        },
        "additionalProperties": false
      },
      "ProgressionQuestState": {
        "type": "object",
        "properties": {
          "identifier": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "summary": {
            "type": "string",
            "nullable": true
          },
          "icon": {
            "type": "string",
            "nullable": true
          },
          "series_identifier": {
            "type": "string",
            "nullable": true
          },
          "series_title": {
            "type": "string",
            "nullable": true
          },
          "series_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "sort_order": {
            "type": "integer",
            "format": "int32"
          },
          "hidden": {
            "type": "boolean"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "is_progress_enabled": {
            "type": "boolean"
          },
          "is_currently_available": {
            "type": "boolean"
          },
          "available_from": {
            "$ref": "#/components/schemas/Instant"
          },
          "available_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "target_count": {
            "type": "integer",
            "format": "int32"
          },
          "progress_count": {
            "type": "integer",
            "format": "int32"
          },
          "series_total_steps": {
            "type": "integer",
            "format": "int32"
          },
          "series_completed_steps": {
            "type": "integer",
            "format": "int32"
          },
          "series_stages": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ProgressionSeriesStage"
            },
            "nullable": true
          },
          "is_completed": {
            "type": "boolean"
          },
          "completed_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "period_key": {
            "type": "string",
            "nullable": true
          },
          "next_reset_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "schedule": {
            "$ref": "#/components/schemas/SnQuestScheduleConfig"
          },
          "reward": {
            "$ref": "#/components/schemas/SnProgressRewardDefinition"
          }
        },
        "additionalProperties": false
      },
      "ProgressionSeriesStage": {
        "type": "object",
        "properties": {
          "identifier": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "series_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "target_count": {
            "type": "integer",
            "format": "int32"
          },
          "is_completed": {
            "type": "boolean"
          },
          "completed_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "PublicAccountConnectionResponse": {
        "type": "object",
        "properties": {
          "provider": {
            "type": "string",
            "nullable": true
          },
          "provided_identifier": {
            "type": "string",
            "nullable": true
          },
          "url": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "QuestDefinitionUpsertRequest": {
        "type": "object",
        "properties": {
          "identifier": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "summary": {
            "type": "string",
            "nullable": true
          },
          "icon": {
            "type": "string",
            "nullable": true
          },
          "series_identifier": {
            "type": "string",
            "nullable": true
          },
          "series_title": {
            "type": "string",
            "nullable": true
          },
          "series_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "sort_order": {
            "type": "integer",
            "format": "int32"
          },
          "hidden": {
            "type": "boolean"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "is_seed_managed": {
            "type": "boolean"
          },
          "is_progress_enabled": {
            "type": "boolean"
          },
          "available_from": {
            "$ref": "#/components/schemas/Instant"
          },
          "available_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "target_count": {
            "type": "integer",
            "format": "int32"
          },
          "trigger": {
            "$ref": "#/components/schemas/SnProgressTriggerDefinition"
          },
          "reward": {
            "$ref": "#/components/schemas/SnProgressRewardDefinition"
          },
          "schedule": {
            "$ref": "#/components/schemas/SnQuestScheduleConfig"
          }
        },
        "additionalProperties": false
      },
      "RealmBoostRequest": {
        "type": "object",
        "properties": {
          "shares": {
            "maximum": 1000000,
            "minimum": 1,
            "type": "integer",
            "format": "int32"
          },
          "currency": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RealmBoostResponse": {
        "type": "object",
        "properties": {
          "order_id": {
            "type": "string",
            "format": "uuid"
          },
          "shares": {
            "type": "integer",
            "format": "int32"
          },
          "currency": {
            "type": "string",
            "nullable": true
          },
          "amount": {
            "type": "number",
            "format": "double"
          }
        },
        "additionalProperties": false
      },
      "RealmLabelAssignmentRequest": {
        "type": "object",
        "properties": {
          "label_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RealmLabelRequest": {
        "required": [
          "name"
        ],
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "color": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RealmMemberProfileRequest": {
        "type": "object",
        "properties": {
          "nick": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RealmMemberRequest": {
        "required": [
          "related_user_id",
          "role"
        ],
        "type": "object",
        "properties": {
          "related_user_id": {
            "type": "string",
            "format": "uuid"
          },
          "role": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "RealmQuotaRecord": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "slug": {
            "type": "string",
            "nullable": true
          },
          "name": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RealmQuotaRecordResourceQuotaResponse": {
        "type": "object",
        "properties": {
          "total": {
            "type": "integer",
            "format": "int32"
          },
          "used": {
            "type": "integer",
            "format": "int32"
          },
          "remaining": {
            "type": "integer",
            "format": "int32"
          },
          "level": {
            "type": "integer",
            "format": "int32"
          },
          "perk_level": {
            "type": "integer",
            "format": "int32"
          },
          "records": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/RealmQuotaRecord"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RealmRequest": {
        "type": "object",
        "properties": {
          "slug": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "picture_id": {
            "type": "string",
            "nullable": true
          },
          "background_id": {
            "type": "string",
            "nullable": true
          },
          "is_community": {
            "type": "boolean",
            "nullable": true
          },
          "is_public": {
            "type": "boolean",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RealmRolePermissionRequest": {
        "required": [
          "can_chat",
          "can_comment",
          "can_manage_members",
          "can_manage_realm",
          "can_moderate_chat",
          "can_moderate_posts",
          "can_post",
          "can_upload_media",
          "role_level"
        ],
        "type": "object",
        "properties": {
          "role_level": {
            "type": "integer",
            "format": "int32"
          },
          "can_chat": {
            "type": "boolean"
          },
          "can_post": {
            "type": "boolean"
          },
          "can_comment": {
            "type": "boolean"
          },
          "can_upload_media": {
            "type": "boolean"
          },
          "can_moderate_posts": {
            "type": "boolean"
          },
          "can_moderate_chat": {
            "type": "boolean"
          },
          "can_manage_members": {
            "type": "boolean"
          },
          "can_manage_realm": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "RealmUserPermissionRequest": {
        "required": [
          "account_id"
        ],
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "can_chat": {
            "type": "boolean",
            "nullable": true
          },
          "can_post": {
            "type": "boolean",
            "nullable": true
          },
          "can_comment": {
            "type": "boolean",
            "nullable": true
          },
          "can_upload_media": {
            "type": "boolean",
            "nullable": true
          },
          "can_moderate_posts": {
            "type": "boolean",
            "nullable": true
          },
          "can_moderate_chat": {
            "type": "boolean",
            "nullable": true
          },
          "can_manage_members": {
            "type": "boolean",
            "nullable": true
          },
          "can_manage_realm": {
            "type": "boolean",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RecoveryPasswordRequest": {
        "required": [
          "account",
          "captcha_token"
        ],
        "type": "object",
        "properties": {
          "account": {
            "minLength": 1,
            "type": "string"
          },
          "captcha_token": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "RecurrenceFrequency": {
        "enum": [
          0,
          1,
          2,
          3,
          4
        ],
        "type": "integer",
        "format": "int32"
      },
      "RecurrencePattern": {
        "type": "object",
        "properties": {
          "frequency": {
            "$ref": "#/components/schemas/RecurrenceFrequency"
          },
          "interval": {
            "type": "integer",
            "format": "int32"
          },
          "end_date": {
            "$ref": "#/components/schemas/Instant"
          },
          "occurrences": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "days_of_week": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/IsoDayOfWeek"
            },
            "nullable": true
          },
          "day_of_month": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "month_of_year": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RegisterTagRequest": {
        "required": [
          "uid"
        ],
        "type": "object",
        "properties": {
          "uid": {
            "maxLength": 64,
            "minLength": 1,
            "type": "string"
          },
          "label": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RelationshipActionRequest": {
        "type": "object",
        "properties": {
          "expires_in": {
            "type": "string",
            "nullable": true
          },
          "degrade_to": {
            "$ref": "#/components/schemas/RelationshipStatus"
          }
        },
        "additionalProperties": false
      },
      "RelationshipRequest": {
        "required": [
          "status"
        ],
        "type": "object",
        "properties": {
          "status": {
            "$ref": "#/components/schemas/RelationshipStatus"
          }
        },
        "additionalProperties": false
      },
      "RelationshipStatus": {
        "enum": [
          0,
          100,
          200,
          -100,
          -50
        ],
        "type": "integer",
        "format": "int32"
      },
      "RelationshipSyncResponse": {
        "type": "object",
        "properties": {
          "added": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountRelationship"
            },
            "nullable": true
          },
          "updated": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountRelationship"
            },
            "nullable": true
          },
          "removed": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "server_timestamp": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ResendAdminMagicSpellRequest": {
        "type": "object",
        "properties": {
          "bypass_verify": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "ResolveObservationRequest": {
        "required": [
          "token"
        ],
        "type": "object",
        "properties": {
          "token": {
            "maxLength": 32,
            "minLength": 32,
            "type": "string"
          },
          "slot": {
            "type": "integer",
            "format": "int64"
          },
          "avg_rssi": {
            "type": "integer",
            "format": "int32"
          },
          "seen_count": {
            "type": "integer",
            "format": "int32"
          },
          "duration_ms": {
            "type": "integer",
            "format": "int64"
          },
          "first_seen_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_seen_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ResolvePeerResponse": {
        "type": "object",
        "properties": {
          "user_id": {
            "type": "string",
            "format": "uuid"
          },
          "display_name": {
            "type": "string",
            "nullable": true
          },
          "avatar": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "is_friend": {
            "type": "boolean"
          },
          "can_invite": {
            "type": "boolean"
          },
          "visibility": {
            "type": "string",
            "nullable": true
          },
          "last_seen_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ResolveRequest": {
        "required": [
          "observations"
        ],
        "type": "object",
        "properties": {
          "observations": {
            "minItems": 1,
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ResolveObservationRequest"
            }
          }
        },
        "additionalProperties": false
      },
      "ResolveResponse": {
        "type": "object",
        "properties": {
          "peers": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ResolvePeerResponse"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SetActivityRequest": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/PresenceType"
          },
          "manual_id": {
            "type": "string",
            "nullable": true
          },
          "provider": {
            "type": "string",
            "nullable": true
          },
          "reference_id": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "subtitle": {
            "type": "string",
            "nullable": true
          },
          "caption": {
            "type": "string",
            "nullable": true
          },
          "large_image": {
            "type": "string",
            "nullable": true
          },
          "small_image": {
            "type": "string",
            "nullable": true
          },
          "title_url": {
            "type": "string",
            "nullable": true
          },
          "subtitle_url": {
            "type": "string",
            "nullable": true
          },
          "queryable_terms": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "lease_minutes": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SetRealmVerificationRequest": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/VerificationMarkType"
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "verified_by": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnAccount": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "nick": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "language": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "region": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_superuser": {
            "type": "boolean"
          },
          "automated_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "profile": {
            "$ref": "#/components/schemas/SnAccountProfile"
          },
          "contacts": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountContact"
            },
            "nullable": true
          },
          "badges": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountBadge"
            },
            "nullable": true
          },
          "perk_subscription": {
            "$ref": "#/components/schemas/SnSubscriptionReferenceObject"
          },
          "perk_level": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SnAccountBadge": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "caption": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountBadgeRef": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "label": {
            "type": "string",
            "nullable": true
          },
          "caption": {
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "activated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountBoardItem": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "kind": {
            "$ref": "#/components/schemas/SnAccountBoardItemKind"
          },
          "widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "custom_app_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "custom_app_widget_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "is_enabled": {
            "type": "boolean"
          },
          "payload": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountBoardItemKind": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "SnAccountConnection": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "provider": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "provided_identifier": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "last_used_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_public": {
            "type": "boolean"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnAccountContact": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/AccountContactType"
          },
          "verified_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_primary": {
            "type": "boolean"
          },
          "is_public": {
            "type": "boolean"
          },
          "content": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAccountProfile": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "first_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "middle_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "last_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "gender": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "pronouns": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "time_zone": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "links": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnProfileLink"
            },
            "nullable": true
          },
          "username_color": {
            "$ref": "#/components/schemas/UsernameColor"
          },
          "birthday": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_seen_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "verification": {
            "$ref": "#/components/schemas/SnVerificationMark"
          },
          "active_badge": {
            "$ref": "#/components/schemas/SnAccountBadgeRef"
          },
          "experience": {
            "type": "integer",
            "format": "int32"
          },
          "level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "leveling_progress": {
            "type": "number",
            "format": "double",
            "readOnly": true
          },
          "social_credits": {
            "type": "number",
            "format": "double"
          },
          "social_credits_level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "picture": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "board": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnAccountBoardItem"
            },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountRelationship": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "related_id": {
            "type": "string",
            "format": "uuid"
          },
          "related": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "degrade_to_status": {
            "$ref": "#/components/schemas/RelationshipStatus"
          },
          "status": {
            "$ref": "#/components/schemas/RelationshipStatus"
          },
          "alias": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnAccountStatus": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "attitude": {
            "$ref": "#/components/schemas/StatusAttitude"
          },
          "is_online": {
            "type": "boolean"
          },
          "is_idle": {
            "type": "boolean"
          },
          "idle_since": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_customized": {
            "type": "boolean"
          },
          "type": {
            "$ref": "#/components/schemas/StatusType"
          },
          "label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "symbol": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "cleared_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "app_identifier": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "is_automated": {
            "type": "boolean"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnAchievementDefinition": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "identifier": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "title": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "summary": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "series_identifier": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "series_title": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "series_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "sort_order": {
            "type": "integer",
            "format": "int32"
          },
          "hidden": {
            "type": "boolean"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "is_seed_managed": {
            "type": "boolean"
          },
          "is_progress_enabled": {
            "type": "boolean"
          },
          "available_from": {
            "$ref": "#/components/schemas/Instant"
          },
          "available_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "target_count": {
            "type": "integer",
            "format": "int32"
          },
          "trigger": {
            "$ref": "#/components/schemas/SnProgressTriggerDefinition"
          },
          "reward": {
            "$ref": "#/components/schemas/SnProgressRewardDefinition"
          }
        },
        "additionalProperties": false
      },
      "SnActionLog": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "action": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "user_agent": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "ip_address": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "location": {
            "$ref": "#/components/schemas/GeoPoint"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "session_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnAffiliationResult": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "resource_identifier": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "spell_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnAffiliationSpell": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "spell": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/AffiliationSpellType"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "affected_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnCalendarEventSubscription": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "subscriber_id": {
            "type": "string",
            "format": "uuid"
          },
          "target_account_id": {
            "type": "string",
            "format": "uuid"
          },
          "notify": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SnCheckInResult": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "level": {
            "$ref": "#/components/schemas/CheckInResultLevel"
          },
          "reward_points": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "reward_experience": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "tips": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/CheckInFortuneTip"
            },
            "nullable": true
          },
          "fortune_report": {
            "$ref": "#/components/schemas/CheckInFortuneReport"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "backdated_from": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnCloudFileReferenceObject": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "nullable": true
          },
          "name": {
            "type": "string",
            "nullable": true
          },
          "file_meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "user_meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "sensitive_marks": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ContentSensitiveMark"
            },
            "nullable": true
          },
          "mime_type": {
            "type": "string",
            "nullable": true
          },
          "hash": {
            "type": "string",
            "nullable": true
          },
          "size": {
            "type": "integer",
            "format": "int64"
          },
          "has_compression": {
            "type": "boolean"
          },
          "url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "width": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "height": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "blurhash": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "usage": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "application_type": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnDomainBlock": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "domain_pattern": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "protocol": {
            "maxLength": 16,
            "type": "string",
            "nullable": true
          },
          "port_restriction": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "reason": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          },
          "trust_level": {
            "$ref": "#/components/schemas/DomainTrustLevel"
          },
          "is_active": {
            "type": "boolean"
          },
          "created_by_account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnExperienceRecord": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "reason_type": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "reason": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "delta": {
            "type": "integer",
            "format": "int64"
          },
          "bonus_multiplier": {
            "type": "number",
            "format": "double"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnLocationPin": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "meet_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "meet": {
            "$ref": "#/components/schemas/SnMeet"
          },
          "device_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "location_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "location_address": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "location_wkt": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "visibility": {
            "$ref": "#/components/schemas/LocationVisibility"
          },
          "status": {
            "$ref": "#/components/schemas/LocationPinStatus"
          },
          "last_heartbeat_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "keep_on_disconnect": {
            "type": "boolean"
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnMagicSpell": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/MagicSpellType"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "affected_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnMeet": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "host_id": {
            "type": "string",
            "format": "uuid"
          },
          "host": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "status": {
            "$ref": "#/components/schemas/MeetStatus"
          },
          "visibility": {
            "$ref": "#/components/schemas/LocationVisibility"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "completed_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "notes": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "location_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "location_address": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "image": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "location_wkt": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "participants": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnMeetParticipant"
            },
            "nullable": true
          },
          "pins": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnLocationPin"
            },
            "nullable": true
          },
          "is_final": {
            "type": "boolean",
            "readOnly": true
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnMeetParticipant": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "meet_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "joined_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnNotableDay": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "local_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "localizable_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "start_date": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_date": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_all_day": {
            "type": "boolean"
          },
          "region": {
            "maxLength": 8,
            "type": "string",
            "nullable": true
          },
          "tags": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/NotableDayTag"
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "is_recurring": {
            "type": "boolean"
          },
          "recurrence_pattern": {
            "maxLength": 16,
            "type": "string",
            "nullable": true
          },
          "is_period": {
            "type": "boolean"
          },
          "holiday_days": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "display_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnPresenceActivity": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/PresenceType"
          },
          "provider": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "reference_id": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "manual_id": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "title": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "subtitle": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "caption": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "large_image": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "small_image": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "title_url": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "subtitle_url": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "queryable_terms": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "lease_minutes": {
            "type": "integer",
            "format": "int32"
          },
          "lease_expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnProfileLink": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "url": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnProgressBadgeRewardDefinition": {
        "type": "object",
        "properties": {
          "type": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "caption": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnProgressRewardDefinition": {
        "type": "object",
        "properties": {
          "experience": {
            "type": "integer",
            "format": "int64"
          },
          "source_points": {
            "type": "number",
            "format": "double"
          },
          "source_points_currency": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "badge": {
            "$ref": "#/components/schemas/SnProgressBadgeRewardDefinition"
          }
        },
        "additionalProperties": false
      },
      "SnProgressRewardGrant": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "definition_type": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "definition_identifier": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "definition_title": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "reward_token": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "source_event_id": {
            "type": "string",
            "format": "uuid"
          },
          "reward": {
            "$ref": "#/components/schemas/SnProgressRewardDefinition"
          },
          "period_key": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "badge_granted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "experience_granted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "source_points_granted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "notification_sent_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnProgressTriggerDefinition": {
        "type": "object",
        "properties": {
          "actions": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "meta_equals": {
            "type": "object",
            "additionalProperties": {
              "type": "string"
            },
            "nullable": true
          },
          "mode": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnQuestDefinition": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "identifier": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "title": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "summary": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "series_identifier": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "series_title": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "series_order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "sort_order": {
            "type": "integer",
            "format": "int32"
          },
          "hidden": {
            "type": "boolean"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "is_seed_managed": {
            "type": "boolean"
          },
          "is_progress_enabled": {
            "type": "boolean"
          },
          "available_from": {
            "$ref": "#/components/schemas/Instant"
          },
          "available_until": {
            "$ref": "#/components/schemas/Instant"
          },
          "target_count": {
            "type": "integer",
            "format": "int32"
          },
          "trigger": {
            "$ref": "#/components/schemas/SnProgressTriggerDefinition"
          },
          "schedule": {
            "$ref": "#/components/schemas/SnQuestScheduleConfig"
          },
          "reward": {
            "$ref": "#/components/schemas/SnProgressRewardDefinition"
          }
        },
        "additionalProperties": false
      },
      "SnQuestScheduleConfig": {
        "type": "object",
        "properties": {
          "repeatability": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "active_days_of_week": {
            "type": "array",
            "items": {
              "type": "integer",
              "format": "int32"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnRealm": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "slug": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "is_community": {
            "type": "boolean"
          },
          "is_public": {
            "type": "boolean"
          },
          "picture": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "verification": {
            "$ref": "#/components/schemas/SnVerificationMark"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "boost_points": {
            "type": "number",
            "format": "double"
          },
          "boost_level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnRealmExperienceRecord": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "reason_type": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "reason": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "delta": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SnRealmLabel": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "color": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "icon": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "created_by_account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnRealmMember": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid"
          },
          "realm": {
            "$ref": "#/components/schemas/SnRealm"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "status": {
            "$ref": "#/components/schemas/SnAccountStatus"
          },
          "nick": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "label_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "label": {
            "$ref": "#/components/schemas/SnRealmLabel"
          },
          "experience": {
            "type": "integer",
            "format": "int32"
          },
          "level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "leveling_progress": {
            "type": "number",
            "format": "double",
            "readOnly": true
          },
          "role": {
            "type": "integer",
            "format": "int32"
          },
          "joined_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "leave_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnRealmRolePermission": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid"
          },
          "realm": {
            "$ref": "#/components/schemas/SnRealm"
          },
          "role_level": {
            "type": "integer",
            "format": "int32"
          },
          "can_chat": {
            "type": "boolean"
          },
          "can_post": {
            "type": "boolean"
          },
          "can_comment": {
            "type": "boolean"
          },
          "can_upload_media": {
            "type": "boolean"
          },
          "can_moderate_posts": {
            "type": "boolean"
          },
          "can_moderate_chat": {
            "type": "boolean"
          },
          "can_manage_members": {
            "type": "boolean"
          },
          "can_manage_realm": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SnRealmUserPermission": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid"
          },
          "realm": {
            "$ref": "#/components/schemas/SnRealm"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "can_chat": {
            "type": "boolean",
            "nullable": true
          },
          "can_post": {
            "type": "boolean",
            "nullable": true
          },
          "can_comment": {
            "type": "boolean",
            "nullable": true
          },
          "can_upload_media": {
            "type": "boolean",
            "nullable": true
          },
          "can_moderate_posts": {
            "type": "boolean",
            "nullable": true
          },
          "can_moderate_chat": {
            "type": "boolean",
            "nullable": true
          },
          "can_manage_members": {
            "type": "boolean",
            "nullable": true
          },
          "can_manage_realm": {
            "type": "boolean",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnRewindPoint": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "year": {
            "type": "integer",
            "format": "int32"
          },
          "schema_version": {
            "type": "integer",
            "format": "int32"
          },
          "sharable_code": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "data": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnSocialCreditRecord": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "reason_type": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "reason": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "delta": {
            "type": "number",
            "format": "double"
          },
          "status": {
            "$ref": "#/components/schemas/SocialCreditRecordStatus"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnSubscriptionReferenceObject": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "identifier": {
            "type": "string",
            "nullable": true
          },
          "group_identifier": {
            "type": "string",
            "nullable": true
          },
          "display_name": {
            "type": "string",
            "nullable": true
          },
          "perk_level": {
            "type": "integer",
            "format": "int32"
          },
          "is_testing": {
            "type": "boolean"
          },
          "begun_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_active": {
            "type": "boolean"
          },
          "is_available": {
            "type": "boolean"
          },
          "is_pending_activation": {
            "type": "boolean"
          },
          "is_free_trial": {
            "type": "boolean"
          },
          "status": {
            "$ref": "#/components/schemas/SubscriptionStatus"
          },
          "base_price": {
            "type": "number",
            "format": "double"
          },
          "final_price": {
            "type": "number",
            "format": "double"
          },
          "renewal_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnTicket": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "title": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/TicketType"
          },
          "status": {
            "$ref": "#/components/schemas/TicketStatus"
          },
          "priority": {
            "$ref": "#/components/schemas/TicketPriority"
          },
          "creator_id": {
            "type": "string",
            "format": "uuid"
          },
          "creator": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "assignee_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "assignee": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "resolved_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "messages": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnTicketMessage"
            },
            "nullable": true
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "resources": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnTicketMessage": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "ticket_id": {
            "type": "string",
            "format": "uuid"
          },
          "sender_id": {
            "type": "string",
            "format": "uuid"
          },
          "sender": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "content": {
            "maxLength": 16384,
            "type": "string",
            "nullable": true
          },
          "files": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnCloudFileReferenceObject"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnUserCalendarEvent": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "title": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "location": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "start_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_all_day": {
            "type": "boolean"
          },
          "visibility": {
            "$ref": "#/components/schemas/EventVisibility"
          },
          "recurrence": {
            "$ref": "#/components/schemas/RecurrencePattern"
          },
          "tags": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "icon": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnVerificationMark": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/VerificationMarkType"
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "verified_by": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SocialCreditRecordStatus": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "StatusAttitude": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "StatusRequest": {
        "type": "object",
        "properties": {
          "attitude": {
            "$ref": "#/components/schemas/StatusAttitude"
          },
          "type": {
            "$ref": "#/components/schemas/StatusType"
          },
          "is_automated": {
            "type": "boolean"
          },
          "label": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "symbol": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "app_identifier": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "icon_id": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "background_id": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "cleared_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "StatusType": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "SteamPresenceScanItem": {
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "steam_id": {
            "type": "string",
            "nullable": true
          },
          "status": {
            "type": "string",
            "nullable": true
          },
          "game_id": {
            "type": "string",
            "nullable": true
          },
          "game_name": {
            "type": "string",
            "nullable": true
          },
          "raw": {
            "nullable": true
          },
          "error": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SteamPresenceScanResult": {
        "type": "object",
        "properties": {
          "items": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SteamPresenceScanItem"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SubscriptionStatus": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "SyncRequest": {
        "required": [
          "last_sync_timestamp"
        ],
        "type": "object",
        "properties": {
          "last_sync_timestamp": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "TicketPriority": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "TicketStatus": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5
        ],
        "type": "integer",
        "format": "int32"
      },
      "TicketType": {
        "enum": [
          0,
          1,
          2,
          3,
          4
        ],
        "type": "integer",
        "format": "int32"
      },
      "TimelineEventType": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "UpdateAccountVerificationRequest": {
        "type": "object",
        "properties": {
          "type": {
            "$ref": "#/components/schemas/VerificationMarkType"
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "verified_by": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateBlockRuleRequest": {
        "type": "object",
        "properties": {
          "domain_pattern": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "protocol": {
            "maxLength": 16,
            "type": "string",
            "nullable": true
          },
          "port_restriction": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "reason": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "priority": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "trust_level": {
            "$ref": "#/components/schemas/DomainTrustLevel"
          },
          "is_active": {
            "type": "boolean",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateCalendarEventRequest": {
        "type": "object",
        "properties": {
          "title": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "location": {
            "type": "string",
            "nullable": true
          },
          "start_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_all_day": {
            "type": "boolean",
            "nullable": true
          },
          "visibility": {
            "$ref": "#/components/schemas/EventVisibility"
          },
          "recurrence": {
            "$ref": "#/components/schemas/RecurrencePattern"
          },
          "tags": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "icon_id": {
            "type": "string",
            "nullable": true
          },
          "background_id": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateLocationRequest": {
        "type": "object",
        "properties": {
          "location_name": {
            "type": "string",
            "nullable": true
          },
          "location_address": {
            "type": "string",
            "nullable": true
          },
          "location_wkt": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateMemberRoleRequest": {
        "type": "object",
        "properties": {
          "role": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "UpdateStatusRequest": {
        "required": [
          "status"
        ],
        "type": "object",
        "properties": {
          "status": {
            "$ref": "#/components/schemas/TicketStatus"
          }
        },
        "additionalProperties": false
      },
      "UpdateTagRequest": {
        "type": "object",
        "properties": {
          "label": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "is_active": {
            "type": "boolean",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateTicketRequest": {
        "type": "object",
        "properties": {
          "title": {
            "maxLength": 256,
            "minLength": 3,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/TicketType"
          },
          "priority": {
            "$ref": "#/components/schemas/TicketPriority"
          },
          "resources": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateVisibilityRequest": {
        "required": [
          "visibility"
        ],
        "type": "object",
        "properties": {
          "visibility": {
            "$ref": "#/components/schemas/LocationVisibility"
          }
        },
        "additionalProperties": false
      },
      "UserCalendarEventDto": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "location": {
            "type": "string",
            "nullable": true
          },
          "start_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_time": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_all_day": {
            "type": "boolean"
          },
          "visibility": {
            "$ref": "#/components/schemas/EventVisibility"
          },
          "recurrence": {
            "$ref": "#/components/schemas/RecurrencePattern"
          },
          "tags": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "icon": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "UsernameColor": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "value": {
            "type": "string",
            "nullable": true
          },
          "direction": {
            "type": "string",
            "nullable": true
          },
          "colors": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ValidateUrlRequest": {
        "required": [
          "url"
        ],
        "type": "object",
        "properties": {
          "url": {
            "minLength": 1,
            "type": "string"
          }
        },
        "additionalProperties": false
      },
      "VerificationMarkType": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6
        ],
        "type": "integer",
        "format": "int32"
      }
    },
    "securitySchemes": {
      "Bearer": {
        "type": "http",
        "description": "Solar Network Unified Authentication",
        "scheme": "Bearer",
        "bearerFormat": "JWT"
      }
    }
  },
  "tags": [
    {
      "name": "Account"
    },
    {
      "name": "AccountAdmin"
    },
    {
      "name": "AccountCurrent"
    },
    {
      "name": "AccountEvent"
    },
    {
      "name": "AccountPublic"
    },
    {
      "name": "AccountRewind"
    },
    {
      "name": "AccountStatsAdmin"
    },
    {
      "name": "AffiliationSpell"
    },
    {
      "name": "BadgesDiscovery"
    },
    {
      "name": "DomainTrust"
    },
    {
      "name": "FortuneSaying"
    },
    {
      "name": "Friends"
    },
    {
      "name": "IpCheck"
    },
    {
      "name": "LocationPin"
    },
    {
      "name": "MagicSpell"
    },
    {
      "name": "Meet"
    },
    {
      "name": "Nearby"
    },
    {
      "name": "Nfc"
    },
    {
      "name": "NfcAdmin"
    },
    {
      "name": "NotableDays"
    },
    {
      "name": "PassKit"
    },
    {
      "name": "PresenceActivity"
    },
    {
      "name": "PresenceArtwork"
    },
    {
      "name": "Progression"
    },
    {
      "name": "ProgressionAdmin"
    },
    {
      "name": "Realm"
    },
    {
      "name": "RealmAdmin"
    },
    {
      "name": "RealmPublic"
    },
    {
      "name": "Relationship"
    },
    {
      "name": "Ticket"
    }
  ]
}
Ring API
DysonNetwork.Ring 服务是负责通知的部份。

以下是自动生成的 API 文档，作为参考用途:{
  "openapi": "3.0.4",
  "info": {
    "title": "DysonNetwork.Ring",
    "description": "The realtime service in the Solar Network.",
    "termsOfService": "https://solsynth.dev/terms",
    "license": {
      "name": "APGLv3",
      "url": "https://www.gnu.org/licenses/agpl-3.0.html"
    },
    "version": "v1"
  },
  "paths": {
    "/ring/admin/delivery-observability/emails": {
      "get": {
        "tags": [
          "DeliveryObservabilityAdmin"
        ],
        "parameters": [
          {
            "name": "from",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "to",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/EmailDeliveryOverview"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailDeliveryOverview"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailDeliveryOverview"
                }
              }
            }
          }
        }
      }
    },
    "/ring/admin/delivery-observability/notifications": {
      "get": {
        "tags": [
          "DeliveryObservabilityAdmin"
        ],
        "parameters": [
          {
            "name": "from",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          },
          {
            "name": "to",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/Instant"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationDeliveryOverview"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationDeliveryOverview"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationDeliveryOverview"
                }
              }
            }
          }
        }
      }
    },
    "/ring/admin/email-plans": {
      "post": {
        "tags": [
          "EmailSendingPlanAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateEmailSendingPlanRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateEmailSendingPlanRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateEmailSendingPlanRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "EmailSendingPlanAdmin"
        ],
        "parameters": [
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 20
            }
          },
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "status",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/EmailSendingPlanStatus"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EmailSendingPlanView"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EmailSendingPlanView"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/EmailSendingPlanView"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/ring/admin/email-plans/{planId}": {
      "get": {
        "tags": [
          "EmailSendingPlanAdmin"
        ],
        "parameters": [
          {
            "name": "planId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              }
            }
          }
        }
      }
    },
    "/ring/admin/email-plans/{planId}/pause": {
      "post": {
        "tags": [
          "EmailSendingPlanAdmin"
        ],
        "parameters": [
          {
            "name": "planId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              }
            }
          }
        }
      }
    },
    "/ring/admin/email-plans/{planId}/resume": {
      "post": {
        "tags": [
          "EmailSendingPlanAdmin"
        ],
        "parameters": [
          {
            "name": "planId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              }
            }
          }
        }
      }
    },
    "/ring/admin/email-plans/{planId}/advance": {
      "post": {
        "tags": [
          "EmailSendingPlanAdmin"
        ],
        "parameters": [
          {
            "name": "planId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/EmailSendingPlanView"
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/count": {
      "get": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "app",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              },
              "application/json": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              },
              "text/json": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications": {
      "get": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 8
            }
          },
          {
            "name": "unmark",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "app",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotification"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotification"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotification"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/all/read": {
      "post": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "app",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/ring/notifications/subscription": {
      "put": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "force",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PushNotificationSubscribeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PushNotificationSubscribeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PushNotificationSubscribeRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotificationPushSubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotificationPushSubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotificationPushSubscription"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "app",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotificationPushSubscription"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotificationPushSubscription"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotificationPushSubscription"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/subscription/current": {
      "get": {
        "tags": [
          "Notification"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotificationPushSubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotificationPushSubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnNotificationPushSubscription"
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/subscription/{subscriptionId}": {
      "delete": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "subscriptionId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              },
              "application/json": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              },
              "text/json": {
                "schema": {
                  "type": "integer",
                  "format": "int32"
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/send": {
      "post": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "save",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "app",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/NotificationWithAimRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/NotificationWithAimRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/NotificationWithAimRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/ring/notifications/preferences": {
      "get": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "app",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotificationPreference"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotificationPreference"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotificationPreference"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/preferences/{topic}": {
      "get": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "topic",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationPreferenceLevel"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationPreferenceLevel"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationPreferenceLevel"
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "topic",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SetPreferenceRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetPreferenceRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetPreferenceRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      },
      "delete": {
        "tags": [
          "Notification"
        ],
        "parameters": [
          {
            "name": "topic",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/ring/admin/stats": {
      "get": {
        "tags": [
          "NotificationStatsAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationStatsResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationStatsResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/NotificationStatsResponse"
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/sop/subscription": {
      "post": {
        "tags": [
          "SopNotification"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/SopRegistrationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SopRegistrationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SopRegistrationRequest"
              }
            }
          }
        },
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SopRegistrationResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SopRegistrationResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SopRegistrationResponse"
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/sop": {
      "get": {
        "tags": [
          "SopNotification"
        ],
        "parameters": [
          {
            "name": "offset",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 0
            }
          },
          {
            "name": "take",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 8
            }
          },
          {
            "name": "app",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotification"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotification"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnNotification"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/ring/notifications/sop/stream": {
      "get": {
        "tags": [
          "SopNotification"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    }
  },
  "components": {
    "schemas": {
      "CreateEmailSendingPlanRequest": {
        "type": "object",
        "properties": {
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "account_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "broadcast_to_all": {
            "type": "boolean"
          },
          "sending_plan_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "subject": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "html_body": {
            "maxLength": 1000000,
            "type": "string",
            "nullable": true
          },
          "planned_start_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "max_emails_per_interval": {
            "maximum": 1000000,
            "minimum": 1,
            "type": "integer",
            "format": "int32"
          },
          "interval_minutes": {
            "maximum": 1440,
            "minimum": 1,
            "type": "integer",
            "format": "int32"
          },
          "max_emails_per_day": {
            "maximum": 1000000,
            "minimum": 1,
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "DeliveryBreakdown": {
        "type": "object",
        "properties": {
          "total": {
            "type": "integer",
            "format": "int64"
          },
          "successful": {
            "type": "integer",
            "format": "int64"
          },
          "failed": {
            "type": "integer",
            "format": "int64"
          },
          "invalid_token": {
            "type": "integer",
            "format": "int64"
          },
          "skipped": {
            "type": "integer",
            "format": "int64"
          },
          "success_rate": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "key": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "DeliverySummary": {
        "type": "object",
        "properties": {
          "total": {
            "type": "integer",
            "format": "int64"
          },
          "successful": {
            "type": "integer",
            "format": "int64"
          },
          "failed": {
            "type": "integer",
            "format": "int64"
          },
          "invalid_token": {
            "type": "integer",
            "format": "int64"
          },
          "skipped": {
            "type": "integer",
            "format": "int64"
          },
          "success_rate": {
            "type": "number",
            "format": "double",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "EmailDeliveryOverview": {
        "type": "object",
        "properties": {
          "summary": {
            "$ref": "#/components/schemas/DeliverySummary"
          },
          "by_source": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/DeliveryBreakdown"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "EmailSendingPlanAdvanceView": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "interval_number": {
            "type": "integer",
            "format": "int32"
          },
          "is_manual": {
            "type": "boolean"
          },
          "attempted_count": {
            "type": "integer",
            "format": "int32"
          },
          "sent_count": {
            "type": "integer",
            "format": "int32"
          },
          "skipped_count": {
            "type": "integer",
            "format": "int32"
          },
          "failed_count": {
            "type": "integer",
            "format": "int32"
          },
          "pending_count_after": {
            "type": "integer",
            "format": "int32"
          },
          "started_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "completed_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "EmailSendingPlanCounts": {
        "type": "object",
        "properties": {
          "total": {
            "type": "integer",
            "format": "int32"
          },
          "pending": {
            "type": "integer",
            "format": "int32"
          },
          "sent": {
            "type": "integer",
            "format": "int32"
          },
          "skipped": {
            "type": "integer",
            "format": "int32"
          },
          "failed": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "EmailSendingPlanStatus": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "EmailSendingPlanView": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "sending_plan_key": {
            "type": "string",
            "nullable": true
          },
          "created_by_account_id": {
            "type": "string",
            "format": "uuid"
          },
          "subject": {
            "type": "string",
            "nullable": true
          },
          "broadcast_to_all": {
            "type": "boolean"
          },
          "recipient_count": {
            "type": "integer",
            "format": "int32"
          },
          "max_emails_per_interval": {
            "type": "integer",
            "format": "int32"
          },
          "interval_minutes": {
            "type": "integer",
            "format": "int32"
          },
          "max_emails_per_day": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "status": {
            "$ref": "#/components/schemas/EmailSendingPlanStatus"
          },
          "advanced_intervals_count": {
            "type": "integer",
            "format": "int32"
          },
          "planned_start_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "next_interval_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_advanced_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "paused_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "completed_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "counts": {
            "$ref": "#/components/schemas/EmailSendingPlanCounts"
          },
          "advances": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/EmailSendingPlanAdvanceView"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "Instant": {
        "type": "object",
        "additionalProperties": false
      },
      "NotificationDeliveryOverview": {
        "type": "object",
        "properties": {
          "send_requests": {
            "type": "integer",
            "format": "int64"
          },
          "send_requests_by_topic": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/DeliveryBreakdown"
            },
            "nullable": true
          },
          "summary": {
            "$ref": "#/components/schemas/DeliverySummary"
          },
          "by_provider": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/DeliveryBreakdown"
            },
            "nullable": true
          },
          "by_topic": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/DeliveryBreakdown"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "NotificationPreferenceLevel": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "NotificationStatsResponse": {
        "type": "object",
        "properties": {
          "calculated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "total_notifications": {
            "type": "integer",
            "format": "int64"
          },
          "unread_notifications": {
            "type": "integer",
            "format": "int64"
          },
          "notifications_last_day": {
            "type": "integer",
            "format": "int64"
          },
          "notifications_last_week": {
            "type": "integer",
            "format": "int64"
          },
          "notifications_last_month": {
            "type": "integer",
            "format": "int64"
          },
          "total_push_subscriptions": {
            "type": "integer",
            "format": "int64"
          },
          "active_push_subscriptions": {
            "type": "integer",
            "format": "int64"
          },
          "total_send_requests": {
            "type": "integer",
            "format": "int64"
          },
          "total_delivery_attempts": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "NotificationWithAimRequest": {
        "required": [
          "account_id",
          "content",
          "title",
          "topic"
        ],
        "type": "object",
        "properties": {
          "topic": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          },
          "title": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          },
          "subtitle": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "content": {
            "maxLength": 4096,
            "minLength": 1,
            "type": "string"
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          },
          "push_type": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            }
          }
        },
        "additionalProperties": false
      },
      "PushNotificationSubscribeRequest": {
        "type": "object",
        "properties": {
          "device_token": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "device_name": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "provider": {
            "$ref": "#/components/schemas/PushProvider"
          },
          "app_id": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PushProvider": {
        "enum": [
          0,
          1,
          2,
          3,
          4
        ],
        "type": "integer",
        "format": "int32"
      },
      "SetPreferenceRequest": {
        "required": [
          "preference"
        ],
        "type": "object",
        "properties": {
          "preference": {
            "$ref": "#/components/schemas/NotificationPreferenceLevel"
          }
        },
        "additionalProperties": false
      },
      "SnNotification": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "topic": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "subtitle": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "content": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": {
              "nullable": true
            },
            "nullable": true
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          },
          "viewed_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "app_id": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "push_type": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnNotificationPreference": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "topic": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "preference": {
            "$ref": "#/components/schemas/NotificationPreferenceLevel"
          }
        },
        "additionalProperties": false
      },
      "SnNotificationPushSubscription": {
        "type": "object",
        "properties": {
          "created_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "updated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "deleted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "device_id": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "device_token": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "device_name": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "provider": {
            "$ref": "#/components/schemas/PushProvider"
          },
          "is_activated": {
            "type": "boolean"
          },
          "app_id": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "count_delivered": {
            "type": "integer",
            "format": "int32"
          },
          "last_used_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SopRegistrationRequest": {
        "type": "object",
        "properties": {
          "device_name": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "app_id": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SopRegistrationResponse": {
        "type": "object",
        "properties": {
          "token": {
            "type": "string",
            "nullable": true
          },
          "subscription": {
            "$ref": "#/components/schemas/SnNotificationPushSubscription"
          }
        },
        "additionalProperties": false
      }
    },
    "securitySchemes": {
      "Bearer": {
        "type": "http",
        "description": "Solar Network Unified Authentication",
        "scheme": "Bearer",
        "bearerFormat": "JWT"
      }
    }
  },
  "tags": [
    {
      "name": "DeliveryObservabilityAdmin"
    },
    {
      "name": "EmailSendingPlanAdmin"
    },
    {
      "name": "Notification"
    },
    {
      "name": "NotificationStatsAdmin"
    },
    {
      "name": "SopNotification"
    }
  ]
}
Sphere API
DysonNetwork.Sphere 服务是社群服务的核心组件，主要负责发布者、帖子等相关的资源；

以下是自动生成的 API 文档，作为参考用途:
