DysonNetwork.Sphere 服务是社群服务的核心组件，主要负责发布者、帖子等相关的资源；

以下是自动生成的 API 文档：
{
  "openapi": "3.0.4",
  "info": {
    "title": "DysonNetwork.Sphere",
    "description": "The social network service in the Solar Network.",
    "termsOfService": "https://solsynth.dev/terms",
    "license": {
      "name": "APGLv3",
      "url": "https://www.gnu.org/licenses/agpl-3.0.html"
    },
    "version": "v1"
  },
  "paths": {
    "/sphere/timeline": {
      "get": {
        "tags": [
          "Activity"
        ],
        "parameters": [
          {
            "name": "cursor",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "filter",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "pub",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "collection",
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
          },
          {
            "name": "mode",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "aggressive",
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
                  "$ref": "#/components/schemas/SnTimelinePage"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTimelinePage"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnTimelinePage"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/actors/{username}": {
      "get": {
        "tags": [
          "ActivityPub"
        ],
        "parameters": [
          {
            "name": "username",
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
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ActivityPubActor"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/actors/{username}/inbox": {
      "post": {
        "tags": [
          "ActivityPub"
        ],
        "parameters": [
          {
            "name": "username",
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
                "type": "object",
                "additionalProperties": { }
              }
            },
            "text/json": {
              "schema": {
                "type": "object",
                "additionalProperties": { }
              }
            },
            "application/*+json": {
              "schema": {
                "type": "object",
                "additionalProperties": { }
              }
            }
          }
        },
        "responses": {
          "202": {
            "description": "Accepted"
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
          },
          "406": {
            "description": "Not Acceptable",
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
    "/sphere/activitypub/objects/{id}": {
      "get": {
        "tags": [
          "ActivityPub"
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
          "404": {
            "description": "Not Found",
            "content": {
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/actors/{username}/outbox": {
      "get": {
        "tags": [
          "ActivityPub"
        ],
        "parameters": [
          {
            "name": "username",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "page",
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
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ActivityPubCollectionPage"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/actors/{username}/followers": {
      "get": {
        "tags": [
          "ActivityPub"
        ],
        "parameters": [
          {
            "name": "username",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "page",
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
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ActivityPubCollectionPage"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/actors/{username}/following": {
      "get": {
        "tags": [
          "ActivityPub"
        ],
        "parameters": [
          {
            "name": "username",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "page",
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
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ActivityPubCollectionPage"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/actors/{username}/featured": {
      "get": {
        "tags": [
          "ActivityPub"
        ],
        "parameters": [
          {
            "name": "username",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "page",
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
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ActivityPubCollectionPage"
                }
              }
            }
          },
          "404": {
            "description": "Not Found",
            "content": {
              "application/activity+json": {
                "schema": {
                  "$ref": "#/components/schemas/ProblemDetails"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/following": {
      "get": {
        "tags": [
          "ActivityPubFollow"
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
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/followers": {
      "get": {
        "tags": [
          "ActivityPubFollow"
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
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/search": {
      "get": {
        "tags": [
          "ActivityPubFollow"
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
            "name": "limit",
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
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/FediverseActorWithFollowStatus"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/relationships": {
      "get": {
        "tags": [
          "ActivityPubFollow"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/RelationshipsSummary"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/RelationshipsSummary"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/RelationshipsSummary"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/check/{username}": {
      "get": {
        "tags": [
          "ActivityPubFollow"
        ],
        "parameters": [
          {
            "name": "username",
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
                  "$ref": "#/components/schemas/ActorCheckResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ActorCheckResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ActorCheckResult"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/activitypub/realms/{slug}": {
      "get": {
        "tags": [
          "ActivityPubRealm"
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
    "/sphere/activitypub/realms/{slug}/followers": {
      "get": {
        "tags": [
          "ActivityPubRealm"
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
            "name": "limit",
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
    "/sphere/activitypub/realms/{slug}/inbox": {
      "post": {
        "tags": [
          "ActivityPubRealm"
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
    "/sphere/activitypub/realms/{slug}/outbox": {
      "get": {
        "tags": [
          "ActivityPubRealm"
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
            "name": "limit",
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
    "/sphere/ads/{name}": {
      "get": {
        "tags": [
          "Ads"
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
                    "$ref": "#/components/schemas/PublicAdvertisingPostStats"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PublicAdvertisingPostStats"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PublicAdvertisingPostStats"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/autocomplete": {
      "post": {
        "tags": [
          "Autocompletion"
        ],
        "parameters": [
          {
            "name": "roomId",
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
    "/sphere/automod/rules": {
      "get": {
        "tags": [
          "Automod"
        ],
        "parameters": [
          {
            "name": "enabledOnly",
            "in": "query",
            "schema": {
              "type": "boolean"
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
                    "$ref": "#/components/schemas/SnAutomodRule"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAutomodRule"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAutomodRule"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Automod"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAutomodRuleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAutomodRuleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAutomodRuleRequest"
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
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/automod/rules/{id}": {
      "get": {
        "tags": [
          "Automod"
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
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "Automod"
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
                "$ref": "#/components/schemas/UpdateAutomodRuleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAutomodRuleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateAutomodRuleRequest"
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
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnAutomodRule"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Automod"
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
    "/sphere/automod/rules/bulk": {
      "post": {
        "tags": [
          "Automod"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/AutomodRuleDto"
                }
              }
            },
            "text/json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/AutomodRuleDto"
                }
              }
            },
            "application/*+json": {
              "schema": {
                "type": "array",
                "items": {
                  "$ref": "#/components/schemas/AutomodRuleDto"
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
                    "$ref": "#/components/schemas/SnAutomodRule"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAutomodRule"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnAutomodRule"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/automod/rules/{id}/test": {
      "post": {
        "tags": [
          "Automod"
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
                "$ref": "#/components/schemas/TestAutomodRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/TestAutomodRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/TestAutomodRequest"
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
                    "$ref": "#/components/schemas/AutomodRuleResult"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AutomodRuleResult"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AutomodRuleResult"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/{username}@{instance}": {
      "get": {
        "tags": [
          "FediverseActor"
        ],
        "parameters": [
          {
            "name": "username",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "instance",
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
                  "$ref": "#/components/schemas/SnFediverseActor"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseActor"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseActor"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/{id}": {
      "get": {
        "tags": [
          "FediverseActor"
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
                  "$ref": "#/components/schemas/SnFediverseActor"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseActor"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseActor"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/search": {
      "get": {
        "tags": [
          "FediverseActor"
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
            "name": "limit",
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
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/{id}/posts": {
      "get": {
        "tags": [
          "FediverseActor"
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
                    "$ref": "#/components/schemas/PostResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PostResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PostResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/{id}/followers": {
      "get": {
        "tags": [
          "FediverseActor"
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
              "default": 40
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
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/{id}/following": {
      "get": {
        "tags": [
          "FediverseActor"
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
              "default": 40
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
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnFediverseActor"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/{id}/relationship": {
      "get": {
        "tags": [
          "FediverseActor"
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
                  "$ref": "#/components/schemas/FediverseRelationshipResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseRelationshipResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseRelationshipResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/availability": {
      "get": {
        "tags": [
          "FediverseActor"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseAvailabilityResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseAvailabilityResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseAvailabilityResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/actors/{id}/follow": {
      "post": {
        "tags": [
          "FediverseActor"
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
    "/sphere/fediverse/actors/{id}/unfollow": {
      "post": {
        "tags": [
          "FediverseActor"
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
    "/sphere/activitypub/actors/{username}/main-key": {
      "get": {
        "tags": [
          "FediverseKey"
        ],
        "parameters": [
          {
            "name": "username",
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
    "/sphere/activitypub/actors/{username}/publickey": {
      "get": {
        "tags": [
          "FediverseKey"
        ],
        "parameters": [
          {
            "name": "username",
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
    "/sphere/admin/fediverse/keys/audit": {
      "get": {
        "tags": [
          "FediverseKeyAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/KeyAuditResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/KeyAuditResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/KeyAuditResult"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/fediverse/keys/backfill": {
      "post": {
        "tags": [
          "FediverseKeyAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/KeyMigrationResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/KeyMigrationResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/KeyMigrationResult"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/fediverse/keys/cleanup": {
      "post": {
        "tags": [
          "FediverseKeyAdmin"
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
    "/sphere/admin/fediverse/keys/stats": {
      "get": {
        "tags": [
          "FediverseKeyAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/KeyStatistics"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/KeyStatistics"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/KeyStatistics"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/fediverse/keys/actor/{actorId}": {
      "get": {
        "tags": [
          "FediverseKeyAdmin"
        ],
        "parameters": [
          {
            "name": "actorId",
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
                  "$ref": "#/components/schemas/ActorKeyInfo"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ActorKeyInfo"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ActorKeyInfo"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/fediverse/keys/actor/{actorId}/regenerate": {
      "post": {
        "tags": [
          "FediverseKeyAdmin"
        ],
        "parameters": [
          {
            "name": "actorId",
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
    "/sphere/fediverse/moderation/rules": {
      "get": {
        "tags": [
          "FediverseModeration"
        ],
        "parameters": [
          {
            "name": "enabledOnly",
            "in": "query",
            "schema": {
              "type": "boolean"
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
                    "$ref": "#/components/schemas/SnFediverseModerationRule"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnFediverseModerationRule"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnFediverseModerationRule"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "FediverseModeration"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateFediverseModerationRuleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateFediverseModerationRuleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateFediverseModerationRuleRequest"
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
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/moderation/rules/{id}": {
      "get": {
        "tags": [
          "FediverseModeration"
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
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "FediverseModeration"
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
                "$ref": "#/components/schemas/UpdateFediverseModerationRuleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateFediverseModerationRuleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateFediverseModerationRuleRequest"
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
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnFediverseModerationRule"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "FediverseModeration"
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
    "/sphere/fediverse/moderation/rules/{id}/toggle": {
      "post": {
        "tags": [
          "FediverseModeration"
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
                "$ref": "#/components/schemas/ToggleRuleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ToggleRuleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ToggleRuleRequest"
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
    "/sphere/fediverse/moderation/check-domain": {
      "post": {
        "tags": [
          "FediverseModeration"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CheckDomainRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CheckDomainRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CheckDomainRequest"
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
                  "$ref": "#/components/schemas/FediverseModerationResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseModerationResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseModerationResult"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/fediverse/moderation/check-actor": {
      "post": {
        "tags": [
          "FediverseModeration"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CheckActorRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CheckActorRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CheckActorRequest"
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
                  "$ref": "#/components/schemas/FediverseModerationResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseModerationResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseModerationResult"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/livestreams": {
      "post": {
        "tags": [
          "LiveStream"
        ],
        "parameters": [
          {
            "name": "pub",
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
                "$ref": "#/components/schemas/CreateLiveStreamRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateLiveStreamRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateLiveStreamRequest"
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
          "LiveStream"
        ],
        "parameters": [
          {
            "name": "limit",
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
            "description": "OK"
          }
        }
      }
    },
    "/sphere/livestreams/publisher/{publisherId}": {
      "get": {
        "tags": [
          "LiveStream"
        ],
        "parameters": [
          {
            "name": "publisherId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "limit",
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
            "description": "OK"
          }
        }
      }
    },
    "/sphere/livestreams/{id}": {
      "get": {
        "tags": [
          "LiveStream"
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
          "LiveStream"
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
                "$ref": "#/components/schemas/UpdateLiveStreamRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateLiveStreamRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateLiveStreamRequest"
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
          "LiveStream"
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
    "/sphere/livestreams/{id}/token": {
      "get": {
        "tags": [
          "LiveStream"
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
            "name": "streamer",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "tool",
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
    "/sphere/livestreams/{id}/start": {
      "post": {
        "tags": [
          "LiveStream"
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
                "$ref": "#/components/schemas/StartStreamingRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StartStreamingRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StartStreamingRequest"
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
    "/sphere/livestreams/{id}/egress": {
      "post": {
        "tags": [
          "LiveStream"
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
                "$ref": "#/components/schemas/StartEgressRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StartEgressRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StartEgressRequest"
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
    "/sphere/livestreams/{id}/egress/stop": {
      "post": {
        "tags": [
          "LiveStream"
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
    "/sphere/livestreams/{id}/hls": {
      "post": {
        "tags": [
          "LiveStream"
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
                "$ref": "#/components/schemas/StartHlsEgressRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StartHlsEgressRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StartHlsEgressRequest"
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
    "/sphere/livestreams/{id}/hls/stop": {
      "post": {
        "tags": [
          "LiveStream"
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
    "/sphere/livestreams/{id}/end": {
      "post": {
        "tags": [
          "LiveStream"
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
    "/sphere/livestreams/{id}/details": {
      "get": {
        "tags": [
          "LiveStream"
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
    "/sphere/livestreams/{id}/thumbnail": {
      "patch": {
        "tags": [
          "LiveStream"
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
                "$ref": "#/components/schemas/UpdateThumbnailRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateThumbnailRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateThumbnailRequest"
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
    "/sphere/livestreams/{id}/chat": {
      "get": {
        "tags": [
          "LiveStream"
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
            "name": "limit",
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
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "LiveStream"
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
                "$ref": "#/components/schemas/SendChatMessageRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SendChatMessageRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SendChatMessageRequest"
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
    "/sphere/livestreams/{id}/chat/{messageId}": {
      "delete": {
        "tags": [
          "LiveStream"
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
            "description": "OK"
          }
        }
      }
    },
    "/sphere/livestreams/{id}/chat/{messageId}/timeout": {
      "post": {
        "tags": [
          "LiveStream"
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
                "$ref": "#/components/schemas/TimeoutRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/TimeoutRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/TimeoutRequest"
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
    "/sphere/livestreams/{id}/awards": {
      "get": {
        "tags": [
          "LiveStream"
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
            "name": "limit",
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
            "description": "OK"
          }
        }
      },
      "post": {
        "tags": [
          "LiveStream"
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
                "$ref": "#/components/schemas/LiveStreamAwardRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/LiveStreamAwardRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/LiveStreamAwardRequest"
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
    "/sphere/livestreams/{id}/awards/active": {
      "get": {
        "tags": [
          "LiveStream"
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
    "/sphere/livestreams/{id}/awards/leaderboard": {
      "get": {
        "tags": [
          "LiveStream"
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
            "name": "limit",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 10
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
    "/sphere/.well-known/nodeinfo": {
      "get": {
        "tags": [
          "NodeInfo"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/.well-known/nodeinfo/2.0": {
      "get": {
        "tags": [
          "NodeInfo"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/.well-known/nodeinfo/2.1": {
      "get": {
        "tags": [
          "NodeInfo"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/posts/featured": {
      "get": {
        "tags": [
          "Post"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/sponsor/current": {
      "get": {
        "tags": [
          "Post"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/posts/sponsor/leaderboard": {
      "get": {
        "tags": [
          "Post"
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
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/posts/{id}/sponsor": {
      "get": {
        "tags": [
          "Post"
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
      "post": {
        "tags": [
          "PostAction"
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
                "$ref": "#/components/schemas/PostSponsorRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PostSponsorRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PostSponsorRequest"
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
                  "$ref": "#/components/schemas/PostSponsorResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PostSponsorResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PostSponsorResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/sponsor/history": {
      "get": {
        "tags": [
          "Post"
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
    "/sphere/posts/drafts": {
      "get": {
        "tags": [
          "Post"
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
          },
          {
            "name": "pub",
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
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/bookmarks": {
      "get": {
        "tags": [
          "Post"
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
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts": {
      "get": {
        "tags": [
          "Post"
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
          },
          {
            "name": "pub",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "realm",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "categories",
            "in": "query",
            "schema": {
              "type": "array",
              "items": {
                "type": "string"
              }
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
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "media",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "shuffle",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "replies",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "pinned",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "order",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "orderDesc",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": true
            }
          },
          {
            "name": "periodStart",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "periodEnd",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "mentioned",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "searchEngine",
            "in": "query",
            "schema": {
              "type": "string"
            }
          }
        ],
        "responses": {
          "200": {
            "description": "Successfully retrieved the list of posts",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          },
          "400": {
            "description": "Invalid request parameters"
          }
        }
      },
      "post": {
        "tags": [
          "PostAction"
        ],
        "parameters": [
          {
            "name": "pub",
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
                "$ref": "#/components/schemas/PostRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PostRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PostRequest"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{publisherName}/{slug}": {
      "get": {
        "tags": [
          "Post"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}": {
      "get": {
        "tags": [
          "Post"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PostAction"
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
            "name": "pub",
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
                "$ref": "#/components/schemas/PostRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PostRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PostRequest"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostAction"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/prev": {
      "get": {
        "tags": [
          "Post"
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
            "name": "pub",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "realm",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "categories",
            "in": "query",
            "schema": {
              "type": "array",
              "items": {
                "type": "string"
              }
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
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "media",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "replies",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "pinned",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "periodStart",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "periodEnd",
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/next": {
      "get": {
        "tags": [
          "Post"
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
            "name": "pub",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "realm",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "type",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "categories",
            "in": "query",
            "schema": {
              "type": "array",
              "items": {
                "type": "string"
              }
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
            "name": "query",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "media",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": false
            }
          },
          {
            "name": "replies",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "pinned",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "periodStart",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32"
            }
          },
          {
            "name": "periodEnd",
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/reactions": {
      "get": {
        "tags": [
          "Post"
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
                    "$ref": "#/components/schemas/SnPostReaction"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostReaction"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostReaction"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PostAction"
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
                "$ref": "#/components/schemas/PostReactionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PostReactionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PostReactionRequest"
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
                  "$ref": "#/components/schemas/SnPostReaction"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostReaction"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostReaction"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/reactions/users/{name}": {
      "get": {
        "tags": [
          "Post"
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
                    "$ref": "#/components/schemas/UserReactionListingItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/UserReactionListingItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/UserReactionListingItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/replies/featured": {
      "get": {
        "tags": [
          "Post"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/replies/pinned": {
      "get": {
        "tags": [
          "Post"
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
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/replies": {
      "get": {
        "tags": [
          "Post"
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
              "default": 20
            }
          },
          {
            "name": "order",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "orderDesc",
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
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/replies/threaded": {
      "get": {
        "tags": [
          "Post"
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
              "default": 20
            }
          },
          {
            "name": "order",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "orderDesc",
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
                    "$ref": "#/components/schemas/ThreadedReplyNode"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/ThreadedReplyNode"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/ThreadedReplyNode"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/thread": {
      "get": {
        "tags": [
          "Post"
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
            "name": "ancestors",
            "in": "query",
            "schema": {
              "type": "boolean",
              "default": true
            }
          },
          {
            "name": "ancestorLimit",
            "in": "query",
            "schema": {
              "type": "integer",
              "format": "int32",
              "default": 50
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
                  "$ref": "#/components/schemas/PostThreadResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PostThreadResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PostThreadResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/forwards": {
      "get": {
        "tags": [
          "Post"
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
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/blog/check-permission": {
      "post": {
        "tags": [
          "PostAction"
        ],
        "parameters": [
          {
            "name": "pub",
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
                "$ref": "#/components/schemas/BlogPermissionCheckRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BlogPermissionCheckRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BlogPermissionCheckRequest"
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
    "/sphere/posts/{id}/bookmark": {
      "post": {
        "tags": [
          "PostAction"
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
                  "$ref": "#/components/schemas/SnPostBookmark"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostBookmark"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostBookmark"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostAction"
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
      "get": {
        "tags": [
          "PostAction"
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
                  "$ref": "#/components/schemas/SnPostBookmark"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostBookmark"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostBookmark"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/awards": {
      "get": {
        "tags": [
          "PostAction"
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
                  "$ref": "#/components/schemas/SnPostAward"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostAward"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostAward"
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PostAction"
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
                "$ref": "#/components/schemas/PostAwardRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PostAwardRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PostAwardRequest"
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
                  "$ref": "#/components/schemas/PostAwardResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PostAwardResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PostAwardResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/awards/pending": {
      "get": {
        "tags": [
          "PostAction"
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
    "/sphere/posts/{id}/pin": {
      "post": {
        "tags": [
          "PostAction"
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
                "$ref": "#/components/schemas/PostPinRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PostPinRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PostPinRequest"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostAction"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/batch/delete": {
      "post": {
        "tags": [
          "PostAction"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchDeleteRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchDeleteRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BatchDeleteRequest"
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
    "/sphere/posts/batch/visibility": {
      "post": {
        "tags": [
          "PostAction"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchVisibilityRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchVisibilityRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BatchVisibilityRequest"
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
    "/sphere/posts/{id}/publish": {
      "post": {
        "tags": [
          "PostAction"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/boost": {
      "post": {
        "tags": [
          "PostAction"
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
                "$ref": "#/components/schemas/BoostRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BoostRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BoostRequest"
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
                  "$ref": "#/components/schemas/SnBoost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnBoost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnBoost"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostAction"
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
    "/sphere/posts/{id}/boosts": {
      "get": {
        "tags": [
          "PostAction"
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
                    "$ref": "#/components/schemas/SnBoost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnBoost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnBoost"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/realm/moderate": {
      "post": {
        "tags": [
          "PostAction"
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
                "$ref": "#/components/schemas/ModeratePostRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ModeratePostRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ModeratePostRequest"
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
    "/sphere/admin/posts": {
      "get": {
        "tags": [
          "PostAdmin"
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
            "name": "publisherId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "realmId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "visibility",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/PostVisibility"
            }
          },
          {
            "name": "shadowbanReason",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/PostShadowbanReason"
            }
          },
          {
            "name": "locked",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "drafted",
            "in": "query",
            "schema": {
              "type": "boolean"
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
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/posts/{id}": {
      "get": {
        "tags": [
          "PostAdmin"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostAdmin"
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
    "/sphere/admin/posts/{id}/lock": {
      "get": {
        "tags": [
          "PostAdmin"
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
      "post": {
        "tags": [
          "PostAdmin"
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
      "delete": {
        "tags": [
          "PostAdmin"
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
    "/sphere/admin/posts/{id}/visibility": {
      "post": {
        "tags": [
          "PostAdmin"
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
                "$ref": "#/components/schemas/SetPostVisibilityRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetPostVisibilityRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetPostVisibilityRequest"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/posts/{id}/shadowban": {
      "post": {
        "tags": [
          "PostAdmin"
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
                "$ref": "#/components/schemas/SetPostShadowbanRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetPostShadowbanRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetPostShadowbanRequest"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostAdmin"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/posts/{id}/realm/remove": {
      "post": {
        "tags": [
          "PostAdmin"
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
                "$ref": "#/components/schemas/RemovePostFromRealmRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RemovePostFromRealmRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RemovePostFromRealmRequest"
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/posts/{id}/lock/batch": {
      "post": {
        "tags": [
          "PostAdmin"
        ],
        "parameters": [
          {
            "name": "id",
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
            },
            "application/*+json": {
              "schema": {
                "type": "array",
                "items": {
                  "type": "string",
                  "format": "uuid"
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
    "/sphere/admin/posts/lock/batch": {
      "delete": {
        "tags": [
          "PostAdmin"
        ],
        "requestBody": {
          "content": {
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
            },
            "application/*+json": {
              "schema": {
                "type": "array",
                "items": {
                  "type": "string",
                  "format": "uuid"
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
    "/sphere/posts/categories": {
      "get": {
        "tags": [
          "PostCategory"
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
                    "$ref": "#/components/schemas/SnPostCategory"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCategory"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCategory"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags": {
      "get": {
        "tags": [
          "PostCategory"
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
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PostTag"
        ],
        "parameters": [
          {
            "name": "pub",
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
                "$ref": "#/components/schemas/CreateTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateTagRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/categories/{slug}": {
      "get": {
        "tags": [
          "PostCategory"
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
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/categories/{slug}/subscribe": {
      "post": {
        "tags": [
          "PostCategory"
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
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/categories/{slug}/unsubscribe": {
      "post": {
        "tags": [
          "PostCategory"
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
    "/sphere/posts/categories/{slug}/subscription": {
      "get": {
        "tags": [
          "PostCategory"
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
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags/{slug}/subscribe": {
      "post": {
        "tags": [
          "PostCategory"
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
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags/{slug}/unsubscribe": {
      "post": {
        "tags": [
          "PostCategory"
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
    "/sphere/posts/tags/{slug}/subscription": {
      "get": {
        "tags": [
          "PostCategory"
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
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/categories": {
      "get": {
        "tags": [
          "PostCategoryAdmin"
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
            "name": "order",
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
                    "$ref": "#/components/schemas/SnPostCategory"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCategory"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCategory"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PostCategoryAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateCategoryRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateCategoryRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateCategoryRequest"
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
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/categories/{slug}": {
      "get": {
        "tags": [
          "PostCategoryAdmin"
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
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PostCategoryAdmin"
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
                "$ref": "#/components/schemas/UpdateCategoryRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCategoryRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCategoryRequest"
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
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategory"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostCategoryAdmin"
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
    "/sphere/categories/subscriptions": {
      "get": {
        "tags": [
          "PostCategorySub"
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
                    "$ref": "#/components/schemas/SnPostCategorySubscription"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCategorySubscription"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCategorySubscription"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/collections": {
      "get": {
        "tags": [
          "PostCollection"
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
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{publisherName}/collections": {
      "get": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
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
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
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
                "$ref": "#/components/schemas/CreateCollectionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateCollectionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateCollectionRequest"
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
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{publisherName}/collections/{slug}": {
      "get": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                "$ref": "#/components/schemas/UpdateCollectionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCollectionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateCollectionRequest"
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
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
    "/sphere/publishers/{publisherName}/collections/{slug}/subscribe": {
      "post": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{publisherName}/collections/{slug}/unsubscribe": {
      "post": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
    "/sphere/publishers/{publisherName}/collections/{slug}/subscription": {
      "get": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCategorySubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{publisherName}/collections/{slug}/posts": {
      "get": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPost"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                "$ref": "#/components/schemas/AddCollectionPostRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AddCollectionPostRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AddCollectionPostRequest"
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
    "/sphere/publishers/{publisherName}/collections/{slug}/posts/batch": {
      "post": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                "$ref": "#/components/schemas/BatchAddCollectionPostsRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchAddCollectionPostsRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BatchAddCollectionPostsRequest"
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
    "/sphere/publishers/{publisherName}/collections/{slug}/posts/batch/remove": {
      "post": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                "$ref": "#/components/schemas/BatchRemoveCollectionPostsRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchRemoveCollectionPostsRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BatchRemoveCollectionPostsRequest"
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
    "/sphere/publishers/{publisherName}/collections/{slug}/posts/{postId}": {
      "delete": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "postId",
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
    "/sphere/publishers/{publisherName}/collections/{slug}/posts/reorder": {
      "put": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
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
                "$ref": "#/components/schemas/ReorderCollectionPostsRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ReorderCollectionPostsRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ReorderCollectionPostsRequest"
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
    "/sphere/publishers/{publisherName}/collections/{slug}/posts/{postId}/prev": {
      "get": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "postId",
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{publisherName}/collections/{slug}/posts/{postId}/next": {
      "get": {
        "tags": [
          "PostCollection"
        ],
        "parameters": [
          {
            "name": "publisherName",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "slug",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "postId",
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
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPost"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/collections": {
      "get": {
        "tags": [
          "PostCollectionAdmin"
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
            "name": "publisherId",
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
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostCollection"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/collections/{id}": {
      "get": {
        "tags": [
          "PostCollectionAdmin"
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
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PostCollectionAdmin"
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
                "$ref": "#/components/schemas/AdminUpdateCollectionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateCollectionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateCollectionRequest"
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
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostCollection"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostCollectionAdmin"
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
    "/sphere/admin/stats": {
      "get": {
        "tags": [
          "PostStatsAdmin"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PostStatsResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PostStatsResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PostStatsResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/subscribe": {
      "post": {
        "tags": [
          "PostSubscription"
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
                "$ref": "#/components/schemas/PostSubscriptionRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PostSubscriptionRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PostSubscriptionRequest"
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
                  "$ref": "#/components/schemas/SnPostSubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostSubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostSubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/{id}/unsubscribe": {
      "post": {
        "tags": [
          "PostSubscription"
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
    "/sphere/posts/{id}/subscription": {
      "get": {
        "tags": [
          "PostSubscription"
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
                  "$ref": "#/components/schemas/SnPostSubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostSubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostSubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/subscriptions": {
      "get": {
        "tags": [
          "PostSubscription"
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
                    "$ref": "#/components/schemas/PostSubscriptionListItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PostSubscriptionListItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/PostSubscriptionListItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags/{slug}": {
      "get": {
        "tags": [
          "PostTag"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PostTag"
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
            "name": "pub",
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags/{slug}/claim": {
      "post": {
        "tags": [
          "PostTag"
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
            "name": "pub",
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags/{slug}/release": {
      "post": {
        "tags": [
          "PostTag"
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
            "name": "pub",
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags/{slug}/protect": {
      "patch": {
        "tags": [
          "PostTag"
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
            "name": "pub",
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
                "$ref": "#/components/schemas/SetProtectedRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetProtectedRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetProtectedRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags/quota": {
      "get": {
        "tags": [
          "PostTag"
        ],
        "parameters": [
          {
            "name": "pub",
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
                  "$ref": "#/components/schemas/ProtectedTagQuotaRecordResourceQuotaResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProtectedTagQuotaRecordResourceQuotaResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProtectedTagQuotaRecordResourceQuotaResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/posts/tags/{slug}/quota": {
      "get": {
        "tags": [
          "PostTag"
        ],
        "parameters": [
          {
            "name": "pub",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
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
                  "$ref": "#/components/schemas/ProtectedTagQuotaRecordResourceQuotaResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProtectedTagQuotaRecordResourceQuotaResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ProtectedTagQuotaRecordResourceQuotaResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/posts/tags": {
      "get": {
        "tags": [
          "PostTagAdmin"
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
            "name": "ownerPublisherId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "isProtected",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "isEvent",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "unowned",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "order",
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
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PostTagAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminTagRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/tags": {
      "get": {
        "tags": [
          "PostTagAdmin"
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
            "name": "ownerPublisherId",
            "in": "query",
            "schema": {
              "type": "string",
              "format": "uuid"
            }
          },
          {
            "name": "isProtected",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "isEvent",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "unowned",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "order",
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
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPostTag"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "PostTagAdmin"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateAdminTagRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/posts/tags/{slug}": {
      "get": {
        "tags": [
          "PostTagAdmin"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PostTagAdmin"
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
                "$ref": "#/components/schemas/AdminUpdateTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateTagRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostTagAdmin"
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
    "/sphere/admin/tags/{slug}": {
      "get": {
        "tags": [
          "PostTagAdmin"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PostTagAdmin"
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
                "$ref": "#/components/schemas/AdminUpdateTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdateTagRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostTagAdmin"
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
    "/sphere/admin/posts/tags/{slug}/assign": {
      "post": {
        "tags": [
          "PostTagAdmin"
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
                "$ref": "#/components/schemas/AssignTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AssignTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AssignTagRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostTagAdmin"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/tags/{slug}/assign": {
      "post": {
        "tags": [
          "PostTagAdmin"
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
                "$ref": "#/components/schemas/AssignTagRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AssignTagRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AssignTagRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PostTagAdmin"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/posts/tags/{slug}/protect": {
      "patch": {
        "tags": [
          "PostTagAdmin"
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
                "$ref": "#/components/schemas/AdminSetProtectedRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminSetProtectedRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminSetProtectedRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/tags/{slug}/protect": {
      "patch": {
        "tags": [
          "PostTagAdmin"
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
                "$ref": "#/components/schemas/AdminSetProtectedRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminSetProtectedRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminSetProtectedRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/posts/tags/{slug}/event": {
      "patch": {
        "tags": [
          "PostTagAdmin"
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
                "$ref": "#/components/schemas/SetEventRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetEventRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetEventRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/tags/{slug}/event": {
      "patch": {
        "tags": [
          "PostTagAdmin"
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
                "$ref": "#/components/schemas/SetEventRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetEventRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetEventRequest"
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
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostTag"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/quota": {
      "get": {
        "tags": [
          "Publisher"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/PublisherQuotaRecordResourceQuotaResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PublisherQuotaRecordResourceQuotaResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PublisherQuotaRecordResourceQuotaResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers": {
      "get": {
        "tags": [
          "Publisher"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/invites": {
      "get": {
        "tags": [
          "Publisher"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherMember"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherMember"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherMember"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/invites/{name}": {
      "post": {
        "tags": [
          "Publisher"
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
                "$ref": "#/components/schemas/PublisherMemberRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherMemberRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherMemberRequest"
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
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/invites/{name}/accept": {
      "post": {
        "tags": [
          "Publisher"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/invites/{name}/decline": {
      "post": {
        "tags": [
          "Publisher"
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
    "/sphere/publishers/{name}/members/{memberId}": {
      "delete": {
        "tags": [
          "Publisher"
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
    "/sphere/publishers/{name}/members/me": {
      "delete": {
        "tags": [
          "Publisher"
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
      },
      "get": {
        "tags": [
          "Publisher"
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
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/members/{memberId}/role": {
      "patch": {
        "tags": [
          "Publisher"
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
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherMember"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/individual": {
      "post": {
        "tags": [
          "Publisher"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherRequest"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/organization/{realmSlug}": {
      "post": {
        "tags": [
          "Publisher"
        ],
        "parameters": [
          {
            "name": "realmSlug",
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
                "$ref": "#/components/schemas/PublisherRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherRequest"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}": {
      "patch": {
        "tags": [
          "Publisher"
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
                "$ref": "#/components/schemas/PublisherRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherRequest"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Publisher"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      },
      "get": {
        "tags": [
          "PublisherPublic"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/members": {
      "get": {
        "tags": [
          "Publisher"
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
                    "$ref": "#/components/schemas/SnPublisherMember"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherMember"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherMember"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/features": {
      "get": {
        "tags": [
          "Publisher"
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
                  "type": "object",
                  "additionalProperties": {
                    "type": "boolean"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "object",
                  "additionalProperties": {
                    "type": "boolean"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "object",
                  "additionalProperties": {
                    "type": "boolean"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Publisher"
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
                "$ref": "#/components/schemas/PublisherFeatureRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherFeatureRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PublisherFeatureRequest"
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
                  "$ref": "#/components/schemas/SnPublisherFeature"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherFeature"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherFeature"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Publisher"
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
            "name": "flag",
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
    "/sphere/publishers/{name}/rewards": {
      "get": {
        "tags": [
          "Publisher"
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
                  "$ref": "#/components/schemas/PublisherRewardPreview"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PublisherRewardPreview"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PublisherRewardPreview"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/rating/history": {
      "get": {
        "tags": [
          "Publisher"
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
          }
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/publishers/rewards/settle": {
      "post": {
        "tags": [
          "Publisher"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/publishers/awards/settle": {
      "post": {
        "tags": [
          "Publisher"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/publishers/rewards/resettle": {
      "post": {
        "tags": [
          "Publisher"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/AggressiveResettleRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AggressiveResettleRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AggressiveResettleRequest"
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
    "/sphere/publishers/{name}/fediverse": {
      "get": {
        "tags": [
          "Publisher"
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
                  "$ref": "#/components/schemas/FediverseStatus"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseStatus"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseStatus"
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Publisher"
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
                  "$ref": "#/components/schemas/FediverseStatus"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseStatus"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/FediverseStatus"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Publisher"
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
    "/sphere/publishers/{name}/domains": {
      "get": {
        "tags": [
          "Publisher"
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
                    "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Publisher"
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
                "$ref": "#/components/schemas/AddDomainRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AddDomainRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AddDomainRequest"
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
                  "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/domains/{domainId}": {
      "delete": {
        "tags": [
          "Publisher"
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
            "name": "domainId",
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
    "/sphere/publishers/{name}/domains/{domainId}/recheck": {
      "post": {
        "tags": [
          "Publisher"
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
            "name": "domainId",
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
                  "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherVerifiedDomain"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/ads": {
      "get": {
        "tags": [
          "Publisher"
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
                    "$ref": "#/components/schemas/AdvertisingPostStats"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AdvertisingPostStats"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/AdvertisingPostStats"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/publishers": {
      "get": {
        "tags": [
          "PublisherAdmin"
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
            "name": "type",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/PublisherType"
            }
          },
          {
            "name": "shadowbanReason",
            "in": "query",
            "schema": {
              "$ref": "#/components/schemas/PublisherShadowbanReason"
            }
          },
          {
            "name": "shadowbanned",
            "in": "query",
            "schema": {
              "type": "boolean"
            }
          },
          {
            "name": "gatekept",
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
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/publishers/{name}": {
      "get": {
        "tags": [
          "PublisherAdmin"
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
                  "$ref": "#/components/schemas/AdminPublisherDetail"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminPublisherDetail"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/AdminPublisherDetail"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PublisherAdmin"
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
                "$ref": "#/components/schemas/AdminUpdatePublisherRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdatePublisherRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/AdminUpdatePublisherRequest"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PublisherAdmin"
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
    "/sphere/admin/publishers/{name}/shadowban": {
      "post": {
        "tags": [
          "PublisherAdmin"
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
                "$ref": "#/components/schemas/SetPublisherShadowbanRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetPublisherShadowbanRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetPublisherShadowbanRequest"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PublisherAdmin"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/admin/publishers/{name}/verification": {
      "post": {
        "tags": [
          "PublisherAdmin"
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
                "$ref": "#/components/schemas/SetPublisherVerificationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SetPublisherVerificationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SetPublisherVerificationRequest"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PublisherAdmin"
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
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisher"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/search": {
      "get": {
        "tags": [
          "PublisherPublic"
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
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/heatmap": {
      "get": {
        "tags": [
          "PublisherPublic"
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
                  "$ref": "#/components/schemas/ActivityHeatmap"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/ActivityHeatmap"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/ActivityHeatmap"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/stats": {
      "get": {
        "tags": [
          "PublisherPublic"
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
                  "$ref": "#/components/schemas/PublisherStats"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/PublisherStats"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/PublisherStats"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/of/{accountId}": {
      "get": {
        "tags": [
          "PublisherPublic"
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
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisher"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/rating": {
      "get": {
        "tags": [
          "PublisherPublic"
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
          }
        }
      }
    },
    "/sphere/publishers/leaderboard": {
      "get": {
        "tags": [
          "PublisherPublic"
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
                    "$ref": "#/components/schemas/LeaderboardEntry"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/LeaderboardEntry"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/LeaderboardEntry"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/rating/overview": {
      "get": {
        "tags": [
          "PublisherPublic"
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
                  "$ref": "#/components/schemas/RatingOverview"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/RatingOverview"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/RatingOverview"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/subscription": {
      "get": {
        "tags": [
          "PublisherSubscription"
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
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/subscription/read-status": {
      "get": {
        "tags": [
          "PublisherSubscription"
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
                  "$ref": "#/components/schemas/SubscriptionReadStatus"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionReadStatus"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionReadStatus"
                }
              }
            }
          }
        }
      },
      "put": {
        "tags": [
          "PublisherSubscription"
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
                "$ref": "#/components/schemas/UpdateSubscriptionReadStateRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateSubscriptionReadStateRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateSubscriptionReadStateRequest"
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
                  "$ref": "#/components/schemas/SubscriptionReadStatus"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionReadStatus"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionReadStatus"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/subscribe": {
      "post": {
        "tags": [
          "PublisherSubscription"
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
                "$ref": "#/components/schemas/SubscribeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SubscribeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SubscribeRequest"
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
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/unsubscribe": {
      "post": {
        "tags": [
          "PublisherSubscription"
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
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriptionStatusResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/subscriptions": {
      "get": {
        "tags": [
          "PublisherSubscription"
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
                    "$ref": "#/components/schemas/SubscriptionWithLiveStatus"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SubscriptionWithLiveStatus"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SubscriptionWithLiveStatus"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/subscribers": {
      "get": {
        "tags": [
          "PublisherSubscription"
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
                    "$ref": "#/components/schemas/SubscriberResponse"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SubscriberResponse"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SubscriberResponse"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/subscribers/{accountId}": {
      "post": {
        "tags": [
          "PublisherSubscription"
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
                  "$ref": "#/components/schemas/SubscriberResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriberResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SubscriberResponse"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "PublisherSubscription"
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
    "/sphere/publishers/{name}/subscription/me/notify": {
      "patch": {
        "tags": [
          "PublisherSubscription"
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
                "$ref": "#/components/schemas/UpdateNotifyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateNotifyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/UpdateNotifyRequest"
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
                  "$ref": "#/components/schemas/SnPublisherSubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherSubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherSubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/subscriptions/read-status": {
      "put": {
        "tags": [
          "PublisherSubscription"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/MarkAllSubscriptionsReadResponse"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/MarkAllSubscriptionsReadResponse"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/MarkAllSubscriptionsReadResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/subscriptions/live": {
      "get": {
        "tags": [
          "PublisherSubscription"
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
                    "$ref": "#/components/schemas/SnLiveStream"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLiveStream"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnLiveStream"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/subscription/requests": {
      "get": {
        "tags": [
          "PublisherSubscription"
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
                    "$ref": "#/components/schemas/SnPublisherFollowRequest"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherFollowRequest"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnPublisherFollowRequest"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/subscription/requests/{requestId}/approve": {
      "post": {
        "tags": [
          "PublisherSubscription"
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
            "name": "requestId",
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
                  "$ref": "#/components/schemas/SnPublisherFollowRequest"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherFollowRequest"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherFollowRequest"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/publishers/{name}/subscription/requests/{requestId}/reject": {
      "post": {
        "tags": [
          "PublisherSubscription"
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
            "name": "requestId",
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
                "$ref": "#/components/schemas/RejectFollowRequestBody"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RejectFollowRequestBody"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RejectFollowRequestBody"
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
                  "$ref": "#/components/schemas/SnPublisherFollowRequest"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherFollowRequest"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublisherFollowRequest"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/account/publishing": {
      "get": {
        "tags": [
          "PublishingSettings"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublishingSettings"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublishingSettings"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublishingSettings"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "PublishingSettings"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/PublishingSettingsRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/PublishingSettingsRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/PublishingSettingsRequest"
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
                  "$ref": "#/components/schemas/SnPublishingSettings"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublishingSettings"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPublishingSettings"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/quote-authorizations/{id}": {
      "get": {
        "tags": [
          "QuoteAuthorization"
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
      "delete": {
        "tags": [
          "QuoteAuthorization"
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
    "/sphere/quote-authorizations": {
      "post": {
        "tags": [
          "QuoteAuthorization"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateQuoteAuthorizationRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/CreateQuoteAuthorizationRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/CreateQuoteAuthorizationRequest"
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
    "/sphere/activitypub/actor": {
      "get": {
        "tags": [
          "ServerActor"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/activitypub/actor/outbox": {
      "get": {
        "tags": [
          "ServerActor"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/activitypub/actor/followers": {
      "get": {
        "tags": [
          "ServerActor"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/activitypub/actor/following": {
      "get": {
        "tags": [
          "ServerActor"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/activitypub/actor/main-key": {
      "get": {
        "tags": [
          "ServerActor"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/stickers": {
      "get": {
        "tags": [
          "Sticker"
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
          },
          {
            "name": "pub",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "order",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
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
                    "$ref": "#/components/schemas/StickerPack"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/StickerPack"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/StickerPack"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "pub",
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
                "$ref": "#/components/schemas/StickerPackRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StickerPackRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StickerPackRequest"
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
                  "$ref": "#/components/schemas/StickerPack"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPack"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPack"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/stickers/me": {
      "get": {
        "tags": [
          "Sticker"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/StickerPackOwnership"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/StickerPackOwnership"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/StickerPackOwnership"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/stickers/{id}": {
      "get": {
        "tags": [
          "Sticker"
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
                  "$ref": "#/components/schemas/StickerPack"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPack"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPack"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "Sticker"
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
                "$ref": "#/components/schemas/StickerPackRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StickerPackRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StickerPackRequest"
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
                  "$ref": "#/components/schemas/StickerPack"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPack"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPack"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Sticker"
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
    "/sphere/stickers/by-prefix/{prefix}": {
      "get": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "prefix",
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
                  "$ref": "#/components/schemas/StickerPack"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPack"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPack"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/stickers/{packId}/content": {
      "get": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
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
                    "$ref": "#/components/schemas/SnSticker"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnSticker"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnSticker"
                  }
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
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
                "$ref": "#/components/schemas/StickerRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StickerRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StickerRequest"
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
    "/sphere/stickers/lookup/{identifier}": {
      "get": {
        "tags": [
          "Sticker"
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
                  "$ref": "#/components/schemas/SnSticker"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSticker"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSticker"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/stickers/lookup/{identifier}/open": {
      "get": {
        "tags": [
          "Sticker"
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
                  "$ref": "#/components/schemas/SnSticker"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSticker"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSticker"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/stickers/lookup/batch": {
      "post": {
        "tags": [
          "Sticker"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchStickerLookupRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchStickerLookupRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BatchStickerLookupRequest"
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
                    "$ref": "#/components/schemas/SnStickerBatchLookupItem"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnStickerBatchLookupItem"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnStickerBatchLookupItem"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/stickers/search": {
      "get": {
        "tags": [
          "Sticker"
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
                    "$ref": "#/components/schemas/SnSticker"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnSticker"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnSticker"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/stickers/{packId}/content/{id}": {
      "get": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
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
                  "$ref": "#/components/schemas/SnSticker"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSticker"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSticker"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
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
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/StickerRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/StickerRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/StickerRequest"
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
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
            "in": "path",
            "required": true,
            "schema": {
              "type": "string",
              "format": "uuid"
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
            "description": "OK"
          }
        }
      }
    },
    "/sphere/stickers/{packId}/content/batch/rendering-settings": {
      "patch": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
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
                "$ref": "#/components/schemas/BatchStickerRenderingSettingsRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/BatchStickerRenderingSettingsRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/BatchStickerRenderingSettingsRequest"
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
    "/sphere/stickers/{packId}/own": {
      "get": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
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
                  "$ref": "#/components/schemas/StickerPackOwnership"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPackOwnership"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPackOwnership"
                }
              }
            }
          }
        }
      },
      "post": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
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
                  "$ref": "#/components/schemas/StickerPackOwnership"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPackOwnership"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/StickerPackOwnership"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
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
    "/sphere/stickers/me/order": {
      "patch": {
        "tags": [
          "Sticker"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/ReorderRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ReorderRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ReorderRequest"
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
    "/sphere/stickers/{packId}/content/order": {
      "patch": {
        "tags": [
          "Sticker"
        ],
        "parameters": [
          {
            "name": "packId",
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
                "$ref": "#/components/schemas/ReorderRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/ReorderRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/ReorderRequest"
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
    "/sphere/surveys/{id}": {
      "get": {
        "tags": [
          "Survey"
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
                  "$ref": "#/components/schemas/SurveyWithStats"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SurveyWithStats"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SurveyWithStats"
                }
              }
            }
          }
        }
      },
      "patch": {
        "tags": [
          "Survey"
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
                "$ref": "#/components/schemas/SurveyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SurveyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SurveyRequest"
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
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Survey"
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
    "/sphere/surveys/{id}/answer": {
      "post": {
        "tags": [
          "Survey"
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
                "$ref": "#/components/schemas/SurveyAnswerRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SurveyAnswerRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SurveyAnswerRequest"
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
                  "$ref": "#/components/schemas/SnSurveyAnswer"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurveyAnswer"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurveyAnswer"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "Survey"
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
    "/sphere/surveys/{id}/feedback": {
      "get": {
        "tags": [
          "Survey"
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
                    "$ref": "#/components/schemas/SnSurveyAnswer"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnSurveyAnswer"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnSurveyAnswer"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/surveys/me": {
      "get": {
        "tags": [
          "Survey"
        ],
        "parameters": [
          {
            "name": "pub",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "active",
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
                    "$ref": "#/components/schemas/SnSurvey"
                  }
                }
              },
              "application/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnSurvey"
                  }
                }
              },
              "text/json": {
                "schema": {
                  "type": "array",
                  "items": {
                    "$ref": "#/components/schemas/SnSurvey"
                  }
                }
              }
            }
          }
        }
      }
    },
    "/sphere/surveys": {
      "post": {
        "tags": [
          "Survey"
        ],
        "parameters": [
          {
            "name": "pub",
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
                "$ref": "#/components/schemas/SurveyRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/SurveyRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/SurveyRequest"
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
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/surveys/{id}/publish": {
      "post": {
        "tags": [
          "Survey"
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
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/surveys/{id}/archive": {
      "post": {
        "tags": [
          "Survey"
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
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/surveys/{id}/clone": {
      "post": {
        "tags": [
          "Survey"
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
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurvey"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/surveys/{id}/subscribe": {
      "post": {
        "tags": [
          "Survey"
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
                  "$ref": "#/components/schemas/SnSurveySubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurveySubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurveySubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/surveys/{id}/unsubscribe": {
      "post": {
        "tags": [
          "Survey"
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
    "/sphere/surveys/{id}/subscription": {
      "get": {
        "tags": [
          "Survey"
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
                  "$ref": "#/components/schemas/SnSurveySubscription"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurveySubscription"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnSurveySubscription"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/timeline/discovery/profile": {
      "get": {
        "tags": [
          "TimelineDiscovery"
        ],
        "responses": {
          "200": {
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/SnDiscoveryProfile"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDiscoveryProfile"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDiscoveryProfile"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/timeline/discovery/uninterested": {
      "post": {
        "tags": [
          "TimelineDiscovery"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/DiscoveryPreferenceRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/DiscoveryPreferenceRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/DiscoveryPreferenceRequest"
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
                  "$ref": "#/components/schemas/SnDiscoveryPreference"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDiscoveryPreference"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnDiscoveryPreference"
                }
              }
            }
          }
        }
      },
      "delete": {
        "tags": [
          "TimelineDiscovery"
        ],
        "parameters": [
          {
            "name": "kind",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "referenceId",
            "in": "query",
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
    "/sphere/timeline/discovery/feedback": {
      "post": {
        "tags": [
          "TimelineDiscovery"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RecommendationFeedbackRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RecommendationFeedbackRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RecommendationFeedbackRequest"
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
                  "$ref": "#/components/schemas/RecommendationFeedbackResult"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/RecommendationFeedbackResult"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/RecommendationFeedbackResult"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/timeline/discovery/weights": {
      "put": {
        "tags": [
          "TimelineDiscovery"
        ],
        "requestBody": {
          "content": {
            "application/json": {
              "schema": {
                "$ref": "#/components/schemas/RecommendationWeightChangeRequest"
              }
            },
            "text/json": {
              "schema": {
                "$ref": "#/components/schemas/RecommendationWeightChangeRequest"
              }
            },
            "application/*+json": {
              "schema": {
                "$ref": "#/components/schemas/RecommendationWeightChangeRequest"
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
                  "$ref": "#/components/schemas/SnPostInterestProfile"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostInterestProfile"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/SnPostInterestProfile"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/timeline/discovery/reset": {
      "post": {
        "tags": [
          "TimelineDiscovery"
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
    "/sphere/translate": {
      "post": {
        "tags": [
          "Translation"
        ],
        "parameters": [
          {
            "name": "to",
            "in": "query",
            "schema": {
              "type": "string"
            }
          },
          {
            "name": "from",
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
    "/sphere/.well-known/host-meta": {
      "get": {
        "tags": [
          "WebFinger"
        ],
        "responses": {
          "200": {
            "description": "OK"
          }
        }
      }
    },
    "/sphere/.well-known/webfinger": {
      "get": {
        "tags": [
          "WebFinger"
        ],
        "parameters": [
          {
            "name": "resource",
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
              "application/jrd+json": {
                "schema": {
                  "$ref": "#/components/schemas/WebFingerResponse"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/scrap/link": {
      "get": {
        "tags": [
          "WebReader"
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
            "description": "OK",
            "content": {
              "text/plain": {
                "schema": {
                  "$ref": "#/components/schemas/LinkEmbed"
                }
              },
              "application/json": {
                "schema": {
                  "$ref": "#/components/schemas/LinkEmbed"
                }
              },
              "text/json": {
                "schema": {
                  "$ref": "#/components/schemas/LinkEmbed"
                }
              }
            }
          }
        }
      }
    },
    "/sphere/scrap/link/cache": {
      "delete": {
        "tags": [
          "WebReader"
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
    "/sphere/scrap/cache/all": {
      "delete": {
        "tags": [
          "WebReader"
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
      "ActivityHeatmap": {
        "type": "object",
        "properties": {
          "unit": {
            "type": "string",
            "nullable": true
          },
          "period_start": {
            "$ref": "#/components/schemas/Instant"
          },
          "period_end": {
            "$ref": "#/components/schemas/Instant"
          },
          "items": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ActivityHeatmapItem"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ActivityHeatmapItem": {
        "type": "object",
        "properties": {
          "date": {
            "$ref": "#/components/schemas/Instant"
          },
          "count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "ActivityPubActor": {
        "type": "object",
        "properties": {
          "@context": {
            "type": "array",
            "items": { },
            "nullable": true
          },
          "id": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "name": {
            "type": "string",
            "nullable": true
          },
          "preferredUsername": {
            "type": "string",
            "nullable": true
          },
          "summary": {
            "type": "string",
            "nullable": true
          },
          "inbox": {
            "type": "string",
            "nullable": true
          },
          "outbox": {
            "type": "string",
            "nullable": true
          },
          "featured": {
            "type": "string",
            "nullable": true
          },
          "followers": {
            "type": "string",
            "nullable": true
          },
          "following": {
            "type": "string",
            "nullable": true
          },
          "published": {
            "$ref": "#/components/schemas/Instant"
          },
          "url": {
            "type": "string",
            "nullable": true
          },
          "icon": {
            "$ref": "#/components/schemas/ActivityPubImage"
          },
          "image": {
            "$ref": "#/components/schemas/ActivityPubImage"
          },
          "publicKey": {
            "$ref": "#/components/schemas/ActivityPubPublicKey"
          }
        },
        "additionalProperties": false
      },
      "ActivityPubCollectionPage": {
        "type": "object",
        "properties": {
          "@context": {
            "type": "array",
            "items": { },
            "nullable": true
          },
          "id": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "totalItems": {
            "type": "integer",
            "format": "int32"
          },
          "partOf": {
            "type": "string",
            "nullable": true
          },
          "orderedItems": {
            "type": "array",
            "items": { },
            "nullable": true
          },
          "next": {
            "type": "string",
            "nullable": true
          },
          "prev": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ActivityPubImage": {
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true
          },
          "mediaType": {
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
      "ActivityPubPublicKey": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "nullable": true
          },
          "owner": {
            "type": "string",
            "nullable": true
          },
          "publicKeyPem": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ActorCheckResult": {
        "type": "object",
        "properties": {
          "exists": {
            "type": "boolean"
          },
          "actor": {
            "$ref": "#/components/schemas/SnFediverseActor"
          },
          "actor_uri": {
            "type": "string",
            "nullable": true
          },
          "username": {
            "type": "string",
            "nullable": true
          },
          "display_name": {
            "type": "string",
            "nullable": true
          },
          "bio": {
            "type": "string",
            "nullable": true
          },
          "avatar_url": {
            "type": "string",
            "nullable": true
          },
          "instance_domain": {
            "type": "string",
            "nullable": true
          },
          "public_key_exists": {
            "type": "boolean"
          },
          "is_local": {
            "type": "boolean"
          },
          "is_discoverable": {
            "type": "boolean"
          },
          "last_activity_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "error": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ActorKeyInfo": {
        "type": "object",
        "properties": {
          "actor_id": {
            "type": "string",
            "format": "uuid"
          },
          "actor_uri": {
            "type": "string",
            "nullable": true
          },
          "username": {
            "type": "string",
            "nullable": true
          },
          "domain": {
            "type": "string",
            "nullable": true
          },
          "has_key": {
            "type": "boolean"
          },
          "has_private_key": {
            "type": "boolean"
          },
          "key_id": {
            "type": "string",
            "nullable": true
          },
          "key_created_at": {
            "type": "string",
            "format": "date-time",
            "nullable": true
          },
          "key_rotated_at": {
            "type": "string",
            "format": "date-time",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AddCollectionPostRequest": {
        "type": "object",
        "properties": {
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "order": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AddDomainRequest": {
        "type": "object",
        "properties": {
          "domain": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminPublisherDetail": {
        "type": "object",
        "properties": {
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "member_count": {
            "type": "integer",
            "format": "int32"
          },
          "post_count": {
            "type": "integer",
            "format": "int32"
          },
          "collection_count": {
            "type": "integer",
            "format": "int32"
          },
          "subscriber_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "AdminSetProtectedRequest": {
        "type": "object",
        "properties": {
          "is_protected": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "AdminUpdateCollectionRequest": {
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminUpdatePublisherRequest": {
        "type": "object",
        "properties": {
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
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "gatekept_follows": {
            "type": "boolean",
            "nullable": true
          },
          "moderate_subscription": {
            "type": "boolean",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdminUpdateTagRequest": {
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AdvertisingPostStats": {
        "type": "object",
        "properties": {
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "slug": {
            "type": "string",
            "nullable": true
          },
          "active_bid_total": {
            "type": "number",
            "format": "double"
          },
          "bid_count": {
            "type": "integer",
            "format": "int32"
          },
          "is_currently_placed": {
            "type": "boolean"
          },
          "shown_count": {
            "type": "integer",
            "format": "int64"
          },
          "last_shown_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "AggressiveResettleRequest": {
        "type": "object",
        "properties": {
          "date_from": {
            "$ref": "#/components/schemas/Instant"
          },
          "date_to": {
            "$ref": "#/components/schemas/Instant"
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AssignTagRequest": {
        "type": "object",
        "properties": {
          "publisher_id": {
            "type": "string",
            "format": "uuid"
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
      "AutomodRuleAction": {
        "enum": [
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "AutomodRuleDto": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/AutomodRuleType"
          },
          "default_action": {
            "$ref": "#/components/schemas/AutomodRuleAction"
          },
          "pattern": {
            "type": "string",
            "nullable": true
          },
          "is_regex": {
            "type": "boolean"
          },
          "derank_weight": {
            "type": "integer",
            "format": "int32"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "AutomodRuleResult": {
        "type": "object",
        "properties": {
          "rule_id": {
            "type": "string",
            "format": "uuid"
          },
          "rule_name": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/AutomodRuleType"
          },
          "action": {
            "$ref": "#/components/schemas/AutomodRuleAction"
          },
          "derank_weight": {
            "type": "integer",
            "format": "int32"
          },
          "matched_text": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "AutomodRuleType": {
        "enum": [
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "BatchAddCollectionPostsRequest": {
        "type": "object",
        "properties": {
          "post_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BatchDeleteRequest": {
        "type": "object",
        "properties": {
          "post_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BatchRemoveCollectionPostsRequest": {
        "type": "object",
        "properties": {
          "post_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BatchStickerLookupRequest": {
        "type": "object",
        "properties": {
          "placeholders": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "BatchStickerRenderingSettingsRequest": {
        "type": "object",
        "properties": {
          "sticker_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "size": {
            "$ref": "#/components/schemas/StickerSize"
          },
          "mode": {
            "$ref": "#/components/schemas/StickerMode"
          }
        },
        "additionalProperties": false
      },
      "BatchVisibilityRequest": {
        "type": "object",
        "properties": {
          "post_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          },
          "visibility": {
            "$ref": "#/components/schemas/PostVisibility"
          },
          "drafted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "published_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "BlogPermissionCheckRequest": {
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
      "BoostInfo": {
        "type": "object",
        "properties": {
          "boost_id": {
            "type": "string",
            "format": "uuid"
          },
          "boosted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "activity_pub_uri": {
            "type": "string",
            "nullable": true
          },
          "web_url": {
            "type": "string",
            "nullable": true
          },
          "original_post": {
            "$ref": "#/components/schemas/SnPost"
          },
          "original_actor": {
            "$ref": "#/components/schemas/SnFediverseActor"
          }
        },
        "additionalProperties": false
      },
      "BoostRequest": {
        "type": "object",
        "properties": {
          "content": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CheckActorRequest": {
        "type": "object",
        "properties": {
          "actor_uri": {
            "type": "string",
            "nullable": true
          },
          "content": {
            "type": "string",
            "nullable": true
          },
          "actor_domain": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CheckDomainRequest": {
        "type": "object",
        "properties": {
          "domain": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ContentMention": {
        "type": "object",
        "properties": {
          "username": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "actor_uri": {
            "maxLength": 2048,
            "type": "string",
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
      "CreateAdminTagRequest": {
        "type": "object",
        "properties": {
          "slug": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
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
          "owner_publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateAutomodRuleRequest": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/AutomodRuleType"
          },
          "default_action": {
            "$ref": "#/components/schemas/AutomodRuleAction"
          },
          "pattern": {
            "type": "string",
            "nullable": true
          },
          "is_regex": {
            "type": "boolean"
          },
          "derank_weight": {
            "type": "integer",
            "format": "int32"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "CreateCategoryRequest": {
        "required": [
          "slug"
        ],
        "type": "object",
        "properties": {
          "slug": {
            "maxLength": 128,
            "minLength": 1,
            "type": "string"
          },
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateCollectionRequest": {
        "type": "object",
        "properties": {
          "slug": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
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
          "background_id": {
            "type": "string",
            "nullable": true
          },
          "icon_id": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateFediverseModerationRuleRequest": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/FediverseModerationRuleType"
          },
          "action": {
            "$ref": "#/components/schemas/FediverseModerationAction"
          },
          "domain": {
            "type": "string",
            "nullable": true
          },
          "keyword_pattern": {
            "type": "string",
            "nullable": true
          },
          "is_regex": {
            "type": "boolean"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "report_threshold": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "CreateLiveStreamRequest": {
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
          "slug": {
            "type": "string",
            "nullable": true
          },
          "thumbnail_id": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/LiveStreamType"
          },
          "visibility": {
            "$ref": "#/components/schemas/LiveStreamVisibility"
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateQuoteAuthorizationRequest": {
        "type": "object",
        "properties": {
          "interacting_object_uri": {
            "type": "string",
            "nullable": true
          },
          "interaction_target_uri": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "CreateTagRequest": {
        "type": "object",
        "properties": {
          "slug": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
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
          }
        },
        "additionalProperties": false
      },
      "DiscoveryPreferenceRequest": {
        "type": "object",
        "properties": {
          "kind": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "reference_id": {
            "type": "string",
            "format": "uuid"
          },
          "reason": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "DiscoveryPreferenceState": {
        "enum": [
          0
        ],
        "type": "integer",
        "format": "int32"
      },
      "DiscoveryTargetKind": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "DomainVerificationStatus": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "DyFediverseContentType": {
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
          9
        ],
        "type": "integer",
        "format": "int32"
      },
      "FediverseActorWithFollowStatus": {
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
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "username": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "display_name": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "inbox_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "outbox_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "followers_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "following_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "featured_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "public_key_id": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "public_key": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "avatar_url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "header_url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "is_bot": {
            "type": "boolean"
          },
          "is_locked": {
            "type": "boolean"
          },
          "is_discoverable": {
            "type": "boolean"
          },
          "is_community": {
            "type": "boolean"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "instance_id": {
            "type": "string",
            "format": "uuid"
          },
          "instance": {
            "$ref": "#/components/schemas/SnFediverseInstance"
          },
          "last_fetched_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_activity_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "outbox_fetched_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "full_handle": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "web_url": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "followers_count": {
            "type": "integer",
            "format": "int32"
          },
          "following_count": {
            "type": "integer",
            "format": "int32"
          },
          "post_count": {
            "type": "integer",
            "format": "int32"
          },
          "total_post_count": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "is_following": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "FediverseAvailabilityResponse": {
        "type": "object",
        "properties": {
          "is_enabled": {
            "type": "boolean"
          },
          "publishers": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/FediversePublisherInfo"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "FediverseModerationAction": {
        "enum": [
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
      "FediverseModerationResult": {
        "type": "object",
        "properties": {
          "is_blocked": {
            "type": "boolean"
          },
          "is_silenced": {
            "type": "boolean"
          },
          "is_suspended": {
            "type": "boolean"
          },
          "is_deranked": {
            "type": "boolean"
          },
          "should_flag": {
            "type": "boolean"
          },
          "matched_rule_name": {
            "type": "string",
            "nullable": true
          },
          "matched_domain": {
            "type": "string",
            "nullable": true
          },
          "matched_keyword": {
            "type": "string",
            "nullable": true
          },
          "action": {
            "$ref": "#/components/schemas/FediverseModerationAction"
          }
        },
        "additionalProperties": false
      },
      "FediverseModerationRuleType": {
        "enum": [
          1,
          2,
          3,
          4,
          5
        ],
        "type": "integer",
        "format": "int32"
      },
      "FediversePublisherInfo": {
        "type": "object",
        "properties": {
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "publisher_name": {
            "type": "string",
            "nullable": true
          },
          "fediverse_handle": {
            "type": "string",
            "nullable": true
          },
          "fediverse_uri": {
            "type": "string",
            "nullable": true
          },
          "avatar_url": {
            "type": "string",
            "nullable": true
          },
          "is_enabled": {
            "type": "boolean"
          },
          "followers_count": {
            "type": "integer",
            "format": "int32"
          },
          "following_count": {
            "type": "integer",
            "format": "int32"
          },
          "posts_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "FediverseRelationshipResponse": {
        "type": "object",
        "properties": {
          "actor_id": {
            "type": "string",
            "format": "uuid"
          },
          "actor_username": {
            "type": "string",
            "nullable": true
          },
          "actor_instance": {
            "type": "string",
            "nullable": true
          },
          "actor_handle": {
            "type": "string",
            "nullable": true
          },
          "is_following": {
            "type": "boolean"
          },
          "is_followed_by": {
            "type": "boolean"
          },
          "is_pending": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "FediverseStatus": {
        "type": "object",
        "properties": {
          "enabled": {
            "type": "boolean"
          },
          "actor": {
            "$ref": "#/components/schemas/SnFediverseActor"
          },
          "follower_count": {
            "type": "integer",
            "format": "int32"
          },
          "actor_uri": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "FollowRequestState": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "Instant": {
        "type": "object",
        "additionalProperties": false
      },
      "KeyAuditResult": {
        "type": "object",
        "properties": {
          "total_actors": {
            "type": "integer",
            "format": "int32"
          },
          "total_keys": {
            "type": "integer",
            "format": "int32"
          },
          "actors_with_keys": {
            "type": "integer",
            "format": "int32"
          },
          "actors_without_keys": {
            "type": "integer",
            "format": "int32"
          },
          "keys_with_private_key": {
            "type": "integer",
            "format": "int32"
          },
          "keys_without_private_key": {
            "type": "integer",
            "format": "int32"
          },
          "orphaned_keys": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "KeyMigrationResult": {
        "type": "object",
        "properties": {
          "migrated_from_actor": {
            "type": "integer",
            "format": "int32"
          },
          "newly_created": {
            "type": "integer",
            "format": "int32"
          },
          "total": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "KeyStatistics": {
        "type": "object",
        "properties": {
          "total_keys": {
            "type": "integer",
            "format": "int32"
          },
          "keys_with_private_key": {
            "type": "integer",
            "format": "int32"
          },
          "keys_without_private_key": {
            "type": "integer",
            "format": "int32"
          },
          "local_actors": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "LeaderboardEntry": {
        "type": "object",
        "properties": {
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "type": "string",
            "nullable": true
          },
          "nick": {
            "type": "string",
            "nullable": true
          },
          "picture": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "rating": {
            "type": "number",
            "format": "double"
          },
          "rank": {
            "type": "integer",
            "format": "int32"
          },
          "percentile": {
            "type": "number",
            "format": "double"
          },
          "grade": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "LinkEmbed": {
        "required": [
          "url"
        ],
        "type": "object",
        "properties": {
          "type": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "url": {
            "type": "string",
            "nullable": true
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "image_url": {
            "type": "string",
            "nullable": true
          },
          "favicon_url": {
            "type": "string",
            "nullable": true
          },
          "site_name": {
            "type": "string",
            "nullable": true
          },
          "content_type": {
            "type": "string",
            "nullable": true
          },
          "author": {
            "type": "string",
            "nullable": true
          },
          "published_date": {
            "type": "string",
            "format": "date-time",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "LiveStreamAwardRequest": {
        "type": "object",
        "properties": {
          "amount": {
            "type": "number",
            "format": "double"
          },
          "message": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "LiveStreamStatus": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "LiveStreamType": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "LiveStreamVisibility": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "MarkAllSubscriptionsReadResponse": {
        "type": "object",
        "properties": {
          "updated_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "ModeratePostRequest": {
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
      "OrderItemRequest": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "order": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "PostAwardRequest": {
        "type": "object",
        "properties": {
          "amount": {
            "type": "number",
            "format": "double"
          },
          "attitude": {
            "$ref": "#/components/schemas/PostReactionAttitude"
          },
          "message": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PostAwardResponse": {
        "type": "object",
        "properties": {
          "order_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "PostContentType": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "PostEmbedView": {
        "type": "object",
        "properties": {
          "uri": {
            "type": "string",
            "nullable": true
          },
          "aspect_ratio": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "renderer": {
            "$ref": "#/components/schemas/PostEmbedViewRenderer"
          }
        },
        "additionalProperties": false
      },
      "PostEmbedViewRenderer": {
        "enum": [
          0
        ],
        "type": "integer",
        "format": "int32"
      },
      "PostInterestKind": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "PostPinMode": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "PostPinRequest": {
        "required": [
          "mode"
        ],
        "type": "object",
        "properties": {
          "mode": {
            "$ref": "#/components/schemas/PostPinMode"
          }
        },
        "additionalProperties": false
      },
      "PostReactionAttitude": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "PostReactionRequest": {
        "type": "object",
        "properties": {
          "symbol": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "attitude": {
            "$ref": "#/components/schemas/PostReactionAttitude"
          }
        },
        "additionalProperties": false
      },
      "PostRequest": {
        "type": "object",
        "properties": {
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "slug": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "content": {
            "type": "string",
            "nullable": true
          },
          "visibility": {
            "$ref": "#/components/schemas/PostVisibility"
          },
          "type": {
            "$ref": "#/components/schemas/PostType"
          },
          "embed_view": {
            "$ref": "#/components/schemas/PostEmbedView"
          },
          "tags": {
            "maxItems": 16,
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "categories": {
            "maxItems": 8,
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "attachments": {
            "maxItems": 32,
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
          "drafted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "published_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "replied_post_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "forwarded_post_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "survey_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "fund_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "meet_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "live_stream_id": {
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
          "thumbnail_id": {
            "type": "string",
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
          "language": {
            "maxLength": 16,
            "type": "string",
            "nullable": true
          },
          "collection_ids": {
            "maxItems": 16,
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PostResponse": {
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
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "slug": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "edited_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "drafted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "published_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "visibility": {
            "$ref": "#/components/schemas/PostVisibility"
          },
          "content": {
            "type": "string",
            "nullable": true
          },
          "content_type": {
            "$ref": "#/components/schemas/PostContentType"
          },
          "type": {
            "$ref": "#/components/schemas/PostType"
          },
          "pin_mode": {
            "$ref": "#/components/schemas/PostPinMode"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "sensitive_marks": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ContentSensitiveMark"
            },
            "nullable": true
          },
          "embed_view": {
            "$ref": "#/components/schemas/PostEmbedView"
          },
          "fediverse_uri": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "fediverse_type": {
            "$ref": "#/components/schemas/DyFediverseContentType"
          },
          "language": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "mentions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ContentMention"
            },
            "nullable": true
          },
          "boost_count": {
            "type": "integer",
            "format": "int32"
          },
          "actor_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "actor": {
            "$ref": "#/components/schemas/SnFediverseActor"
          },
          "views_unique": {
            "type": "integer",
            "format": "int32"
          },
          "views_total": {
            "type": "integer",
            "format": "int32"
          },
          "upvotes": {
            "type": "integer",
            "format": "int32"
          },
          "downvotes": {
            "type": "integer",
            "format": "int32"
          },
          "awarded_score": {
            "type": "number",
            "format": "double"
          },
          "replies_count": {
            "type": "integer",
            "format": "int32"
          },
          "thread_replies_count": {
            "type": "integer",
            "format": "int32"
          },
          "debug_rank": {
            "type": "number",
            "format": "double"
          },
          "sponsored": {
            "type": "boolean"
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
          "is_bookmarked": {
            "type": "boolean"
          },
          "replied_gone": {
            "type": "boolean"
          },
          "forwarded_gone": {
            "type": "boolean"
          },
          "replied_post_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "replied_post": {
            "$ref": "#/components/schemas/SnPost"
          },
          "forwarded_post_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "forwarded_post": {
            "$ref": "#/components/schemas/SnPost"
          },
          "quote_authorization_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "realm": {
            "$ref": "#/components/schemas/SnRealm"
          },
          "attachments": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnCloudFileReferenceObject"
            },
            "nullable": true
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "shadowban_reason": {
            "$ref": "#/components/schemas/PostShadowbanReason"
          },
          "shadowbanned_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "locked_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_shadowbanned": {
            "type": "boolean",
            "readOnly": true
          },
          "awards": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostAward"
            },
            "nullable": true
          },
          "tags": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostTag"
            },
            "nullable": true
          },
          "categories": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostCategory"
            },
            "nullable": true
          },
          "publisher_collections": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostCollection"
            },
            "nullable": true
          },
          "featured_records": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostFeaturedRecord"
            },
            "nullable": true
          },
          "is_truncated": {
            "type": "boolean"
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "boost_info": {
            "$ref": "#/components/schemas/BoostInfo"
          },
          "is_cached": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "PostShadowbanReason": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6,
          99
        ],
        "type": "integer",
        "format": "int32"
      },
      "PostSponsorRequest": {
        "type": "object",
        "properties": {
          "amount": {
            "type": "number",
            "format": "double"
          }
        },
        "additionalProperties": false
      },
      "PostSponsorResponse": {
        "type": "object",
        "properties": {
          "order_id": {
            "type": "string",
            "format": "uuid"
          },
          "amount": {
            "type": "number",
            "format": "double"
          }
        },
        "additionalProperties": false
      },
      "PostStatsResponse": {
        "type": "object",
        "properties": {
          "calculated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "total_posts": {
            "type": "integer",
            "format": "int64"
          },
          "published_posts": {
            "type": "integer",
            "format": "int64"
          },
          "draft_posts": {
            "type": "integer",
            "format": "int64"
          },
          "posts_last_day": {
            "type": "integer",
            "format": "int64"
          },
          "posts_last_week": {
            "type": "integer",
            "format": "int64"
          },
          "posts_last_month": {
            "type": "integer",
            "format": "int64"
          },
          "total_publishers": {
            "type": "integer",
            "format": "int64"
          },
          "total_reactions": {
            "type": "integer",
            "format": "int64"
          },
          "total_bookmarks": {
            "type": "integer",
            "format": "int64"
          }
        },
        "additionalProperties": false
      },
      "PostSubscriptionListItem": {
        "required": [
          "post",
          "subscription"
        ],
        "type": "object",
        "properties": {
          "subscription": {
            "$ref": "#/components/schemas/SnPostSubscription"
          },
          "post": {
            "$ref": "#/components/schemas/SnPost"
          }
        },
        "additionalProperties": false
      },
      "PostSubscriptionRequest": {
        "type": "object",
        "properties": {
          "reactions": {
            "type": "boolean",
            "nullable": true
          },
          "forwards": {
            "type": "boolean",
            "nullable": true
          },
          "edits": {
            "type": "boolean",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PostThreadResponse": {
        "required": [
          "current",
          "descendants",
          "has_more"
        ],
        "type": "object",
        "properties": {
          "ancestors": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ThreadedReplyNode"
            },
            "nullable": true
          },
          "current": {
            "$ref": "#/components/schemas/ThreadedReplyNode"
          },
          "descendants": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ThreadedReplyNode"
            },
            "nullable": true
          },
          "has_more": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "PostType": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "PostVisibility": {
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
      "ProtectedTagQuotaRecord": {
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
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "is_protected": {
            "type": "boolean"
          },
          "is_event": {
            "type": "boolean"
          },
          "event_ends_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "ProtectedTagQuotaRecordResourceQuotaResponse": {
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
              "$ref": "#/components/schemas/ProtectedTagQuotaRecord"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PublicAdvertisingPostStats": {
        "type": "object",
        "properties": {
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "title": {
            "type": "string",
            "nullable": true
          },
          "slug": {
            "type": "string",
            "nullable": true
          },
          "active_bid_total": {
            "type": "number",
            "format": "double"
          },
          "bid_count": {
            "type": "integer",
            "format": "int32"
          },
          "is_currently_placed": {
            "type": "boolean"
          },
          "shown_count": {
            "type": "integer",
            "format": "int64"
          },
          "last_shown_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "display_chance": {
            "type": "number",
            "format": "double"
          }
        },
        "additionalProperties": false
      },
      "PublisherFeatureRequest": {
        "required": [
          "flag"
        ],
        "type": "object",
        "properties": {
          "flag": {
            "minLength": 1,
            "type": "string"
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "PublisherMemberRequest": {
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
            "$ref": "#/components/schemas/PublisherMemberRole"
          }
        },
        "additionalProperties": false
      },
      "PublisherMemberRole": {
        "enum": [
          25,
          50,
          75,
          100
        ],
        "type": "integer",
        "format": "int32"
      },
      "PublisherQuotaRecord": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "name": {
            "type": "string",
            "nullable": true
          },
          "nick": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/PublisherType"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PublisherQuotaRecordResourceQuotaResponse": {
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
              "$ref": "#/components/schemas/PublisherQuotaRecord"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PublisherRequest": {
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "pattern": "^[a-zA-Z0-9](?:[a-zA-Z0-9\\-_\\.]*[a-zA-Z0-9])?$",
            "type": "string",
            "nullable": true
          },
          "nick": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "bio": {
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
          "default_post_tags": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "default_post_categories": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "payout_wallet_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "PublisherRewardPreview": {
        "type": "object",
        "properties": {
          "experience": {
            "type": "integer",
            "format": "int32"
          },
          "social_credits": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "PublisherShadowbanReason": {
        "enum": [
          0,
          1,
          2,
          3,
          4,
          5,
          6,
          99
        ],
        "type": "integer",
        "format": "int32"
      },
      "PublisherStats": {
        "type": "object",
        "properties": {
          "posts_created": {
            "type": "integer",
            "format": "int32"
          },
          "sticker_packs_created": {
            "type": "integer",
            "format": "int32"
          },
          "stickers_created": {
            "type": "integer",
            "format": "int32"
          },
          "upvote_received": {
            "type": "integer",
            "format": "int32"
          },
          "downvote_received": {
            "type": "integer",
            "format": "int32"
          },
          "subscribers_count": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "PublisherType": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "PublishingSettingsRequest": {
        "type": "object",
        "properties": {
          "default_posting_publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "default_reply_publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "default_fediverse_publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RatingOverview": {
        "type": "object",
        "properties": {
          "rating": {
            "type": "number",
            "format": "double"
          },
          "rank": {
            "type": "integer",
            "format": "int32"
          },
          "total_publishers": {
            "type": "integer",
            "format": "int32"
          },
          "percentile": {
            "type": "number",
            "format": "double"
          },
          "grade": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RecommendationFeedbackRequest": {
        "type": "object",
        "properties": {
          "kind": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "reference_id": {
            "type": "string",
            "format": "uuid"
          },
          "feedback": {
            "maxLength": 16,
            "type": "string",
            "nullable": true
          },
          "reason": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "suppress": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "RecommendationFeedbackResult": {
        "type": "object",
        "properties": {
          "updated_profiles": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostInterestProfile"
            },
            "nullable": true
          },
          "preference": {
            "$ref": "#/components/schemas/SnDiscoveryPreference"
          }
        },
        "additionalProperties": false
      },
      "RecommendationWeightChangeRequest": {
        "type": "object",
        "properties": {
          "kind": {
            "maxLength": 32,
            "type": "string",
            "nullable": true
          },
          "reference_id": {
            "type": "string",
            "format": "uuid"
          },
          "score_delta": {
            "type": "number",
            "format": "double"
          },
          "interaction_count": {
            "type": "integer",
            "format": "int32"
          },
          "signal_type": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RejectFollowRequestBody": {
        "type": "object",
        "properties": {
          "reason": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RelationshipState": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "RelationshipSummaryItem": {
        "type": "object",
        "properties": {
          "actor": {
            "$ref": "#/components/schemas/SnFediverseActor"
          },
          "state": {
            "$ref": "#/components/schemas/RelationshipState"
          },
          "is_following": {
            "type": "boolean"
          },
          "followed_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "target_actor_uri": {
            "type": "string",
            "nullable": true
          },
          "username": {
            "type": "string",
            "nullable": true
          },
          "display_name": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RelationshipsSummary": {
        "type": "object",
        "properties": {
          "actor_uri": {
            "type": "string",
            "nullable": true
          },
          "following_count": {
            "type": "integer",
            "format": "int32"
          },
          "followers_count": {
            "type": "integer",
            "format": "int32"
          },
          "pending_count": {
            "type": "integer",
            "format": "int32"
          },
          "relationships": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/RelationshipSummaryItem"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "RemovePostFromRealmRequest": {
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
      "ReorderCollectionPostsRequest": {
        "type": "object",
        "properties": {
          "post_ids": {
            "type": "array",
            "items": {
              "type": "string",
              "format": "uuid"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ReorderRequest": {
        "type": "object",
        "properties": {
          "items": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/OrderItemRequest"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SendChatMessageRequest": {
        "required": [
          "content"
        ],
        "type": "object",
        "properties": {
          "content": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SetEventRequest": {
        "type": "object",
        "properties": {
          "is_event": {
            "type": "boolean"
          },
          "ends_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SetPostShadowbanRequest": {
        "type": "object",
        "properties": {
          "reason": {
            "$ref": "#/components/schemas/PostShadowbanReason"
          }
        },
        "additionalProperties": false
      },
      "SetPostVisibilityRequest": {
        "type": "object",
        "properties": {
          "visibility": {
            "$ref": "#/components/schemas/PostVisibility"
          }
        },
        "additionalProperties": false
      },
      "SetProtectedRequest": {
        "type": "object",
        "properties": {
          "is_protected": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SetPublisherShadowbanRequest": {
        "type": "object",
        "properties": {
          "reason": {
            "$ref": "#/components/schemas/PublisherShadowbanReason"
          }
        },
        "additionalProperties": false
      },
      "SetPublisherVerificationRequest": {
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
      "SnAutomodRule": {
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
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/AutomodRuleType"
          },
          "default_action": {
            "$ref": "#/components/schemas/AutomodRuleAction"
          },
          "pattern": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "is_regex": {
            "type": "boolean"
          },
          "derank_weight": {
            "type": "integer",
            "format": "int32"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SnBoost": {
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
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "actor_id": {
            "type": "string",
            "format": "uuid"
          },
          "activity_pub_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "web_url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "content": {
            "type": "string",
            "nullable": true
          },
          "boosted_at": {
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
      "SnDiscoveryInterestEntry": {
        "type": "object",
        "properties": {
          "kind": {
            "type": "string",
            "nullable": true
          },
          "reference_id": {
            "type": "string",
            "format": "uuid"
          },
          "label": {
            "type": "string",
            "nullable": true
          },
          "score": {
            "type": "number",
            "format": "double"
          },
          "interaction_count": {
            "type": "integer",
            "format": "int32"
          },
          "last_interacted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_signal_type": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnDiscoveryPreference": {
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
          "kind": {
            "$ref": "#/components/schemas/DiscoveryTargetKind"
          },
          "reference_id": {
            "type": "string",
            "format": "uuid"
          },
          "state": {
            "$ref": "#/components/schemas/DiscoveryPreferenceState"
          },
          "reason": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "applied_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnDiscoveryProfile": {
        "type": "object",
        "properties": {
          "generated_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "interests": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnDiscoveryInterestEntry"
            },
            "nullable": true
          },
          "suggested_publishers": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnDiscoverySuggestion"
            },
            "nullable": true
          },
          "suggested_accounts": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnDiscoverySuggestion"
            },
            "nullable": true
          },
          "suggested_realms": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnDiscoverySuggestion"
            },
            "nullable": true
          },
          "suppressed": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnDiscoverySuggestion"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnDiscoverySuggestion": {
        "type": "object",
        "properties": {
          "kind": {
            "$ref": "#/components/schemas/DiscoveryTargetKind"
          },
          "reference_id": {
            "type": "string",
            "format": "uuid"
          },
          "label": {
            "type": "string",
            "nullable": true
          },
          "score": {
            "type": "number",
            "format": "double"
          },
          "reasons": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "data": {
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnFediverseActor": {
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
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "username": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "display_name": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "inbox_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "outbox_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "followers_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "following_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "featured_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "public_key_id": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "public_key": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "avatar_url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "header_url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "is_bot": {
            "type": "boolean"
          },
          "is_locked": {
            "type": "boolean"
          },
          "is_discoverable": {
            "type": "boolean"
          },
          "is_community": {
            "type": "boolean"
          },
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "instance_id": {
            "type": "string",
            "format": "uuid"
          },
          "instance": {
            "$ref": "#/components/schemas/SnFediverseInstance"
          },
          "last_fetched_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_activity_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "outbox_fetched_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "full_handle": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "web_url": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          },
          "followers_count": {
            "type": "integer",
            "format": "int32"
          },
          "following_count": {
            "type": "integer",
            "format": "int32"
          },
          "post_count": {
            "type": "integer",
            "format": "int32"
          },
          "total_post_count": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnFediverseInstance": {
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
          "domain": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "software": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "version": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "icon_url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "thumbnail_url": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "contact_email": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "contact_account_username": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "active_users": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "is_blocked": {
            "type": "boolean"
          },
          "is_silenced": {
            "type": "boolean"
          },
          "block_reason": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "last_fetched_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_activity_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "metadata_fetched_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnFediverseModerationRule": {
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
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/FediverseModerationRuleType"
          },
          "action": {
            "$ref": "#/components/schemas/FediverseModerationAction"
          },
          "domain": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "keyword_pattern": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "is_regex": {
            "type": "boolean"
          },
          "is_enabled": {
            "type": "boolean"
          },
          "report_threshold": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "expires_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "priority": {
            "type": "integer",
            "format": "int32"
          },
          "is_system_rule": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SnLiveStream": {
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
          "slug": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/LiveStreamType"
          },
          "visibility": {
            "$ref": "#/components/schemas/LiveStreamVisibility"
          },
          "status": {
            "$ref": "#/components/schemas/LiveStreamStatus"
          },
          "room_name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "ingress_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "ingress_stream_key": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "egress_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "hls_egress_id": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "hls_playlist_path": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "hls_started_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "started_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "total_duration_seconds": {
            "type": "integer",
            "format": "int64"
          },
          "viewer_count": {
            "type": "integer",
            "format": "int32"
          },
          "peak_viewer_count": {
            "type": "integer",
            "format": "int32"
          },
          "total_award_score": {
            "type": "number",
            "format": "double"
          },
          "distributed_award_amount": {
            "type": "number",
            "format": "double"
          },
          "thumbnail": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnPost": {
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
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "slug": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "edited_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "drafted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "published_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "visibility": {
            "$ref": "#/components/schemas/PostVisibility"
          },
          "content": {
            "type": "string",
            "nullable": true
          },
          "content_type": {
            "$ref": "#/components/schemas/PostContentType"
          },
          "type": {
            "$ref": "#/components/schemas/PostType"
          },
          "pin_mode": {
            "$ref": "#/components/schemas/PostPinMode"
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "sensitive_marks": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ContentSensitiveMark"
            },
            "nullable": true
          },
          "embed_view": {
            "$ref": "#/components/schemas/PostEmbedView"
          },
          "fediverse_uri": {
            "maxLength": 8192,
            "type": "string",
            "nullable": true
          },
          "fediverse_type": {
            "$ref": "#/components/schemas/DyFediverseContentType"
          },
          "language": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "mentions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/ContentMention"
            },
            "nullable": true
          },
          "boost_count": {
            "type": "integer",
            "format": "int32"
          },
          "actor_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "actor": {
            "$ref": "#/components/schemas/SnFediverseActor"
          },
          "views_unique": {
            "type": "integer",
            "format": "int32"
          },
          "views_total": {
            "type": "integer",
            "format": "int32"
          },
          "upvotes": {
            "type": "integer",
            "format": "int32"
          },
          "downvotes": {
            "type": "integer",
            "format": "int32"
          },
          "awarded_score": {
            "type": "number",
            "format": "double"
          },
          "replies_count": {
            "type": "integer",
            "format": "int32"
          },
          "thread_replies_count": {
            "type": "integer",
            "format": "int32"
          },
          "debug_rank": {
            "type": "number",
            "format": "double"
          },
          "sponsored": {
            "type": "boolean"
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
          "is_bookmarked": {
            "type": "boolean"
          },
          "replied_gone": {
            "type": "boolean"
          },
          "forwarded_gone": {
            "type": "boolean"
          },
          "replied_post_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "replied_post": {
            "$ref": "#/components/schemas/SnPost"
          },
          "forwarded_post_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "forwarded_post": {
            "$ref": "#/components/schemas/SnPost"
          },
          "quote_authorization_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "realm": {
            "$ref": "#/components/schemas/SnRealm"
          },
          "attachments": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnCloudFileReferenceObject"
            },
            "nullable": true
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "shadowban_reason": {
            "$ref": "#/components/schemas/PostShadowbanReason"
          },
          "shadowbanned_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "locked_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_shadowbanned": {
            "type": "boolean",
            "readOnly": true
          },
          "awards": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostAward"
            },
            "nullable": true
          },
          "tags": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostTag"
            },
            "nullable": true
          },
          "categories": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostCategory"
            },
            "nullable": true
          },
          "publisher_collections": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostCollection"
            },
            "nullable": true
          },
          "featured_records": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPostFeaturedRecord"
            },
            "nullable": true
          },
          "is_truncated": {
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
      "SnPostAward": {
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
          "amount": {
            "type": "number",
            "format": "double"
          },
          "attitude": {
            "$ref": "#/components/schemas/PostReactionAttitude"
          },
          "message": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "settled_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnPostBookmark": {
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
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnPostCategory": {
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
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "usage": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnPostCategorySubscription": {
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
          "category_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "category": {
            "$ref": "#/components/schemas/SnPostCategory"
          },
          "tag_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "tag": {
            "$ref": "#/components/schemas/SnPostTag"
          },
          "collection_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "collection": {
            "$ref": "#/components/schemas/SnPostCollection"
          }
        },
        "additionalProperties": false
      },
      "SnPostCollection": {
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
            "maxLength": 128,
            "type": "string",
            "nullable": true
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
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "background": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "icon": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "posts": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnPost"
            },
            "nullable": true
          },
          "item_count": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnPostFeaturedRecord": {
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
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "featured_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "social_credits": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SnPostInterestProfile": {
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
          "kind": {
            "$ref": "#/components/schemas/PostInterestKind"
          },
          "reference_id": {
            "type": "string",
            "format": "uuid"
          },
          "score": {
            "type": "number",
            "format": "double"
          },
          "interaction_count": {
            "type": "integer",
            "format": "int32"
          },
          "last_interacted_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_signal_type": {
            "maxLength": 64,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnPostReaction": {
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
          "symbol": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "attitude": {
            "$ref": "#/components/schemas/PostReactionAttitude"
          },
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "fediverse_uri": {
            "maxLength": 2048,
            "type": "string",
            "nullable": true
          },
          "actor_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "actor": {
            "$ref": "#/components/schemas/SnFediverseActor"
          },
          "is_local": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SnPostSubscription": {
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
          "post_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "notify_reactions": {
            "type": "boolean"
          },
          "notify_forwards": {
            "type": "boolean"
          },
          "notify_edits": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SnPostTag": {
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
            "maxLength": 128,
            "type": "string",
            "nullable": true
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
          "owner_publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "owner_publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "is_protected": {
            "type": "boolean"
          },
          "is_event": {
            "type": "boolean"
          },
          "event_ends_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "usage": {
            "type": "integer",
            "format": "int32",
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
      "SnPublisher": {
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
            "$ref": "#/components/schemas/PublisherType"
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
          "bio": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
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
          "realm_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "payout_wallet_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "realm": {
            "$ref": "#/components/schemas/SnRealm"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
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
          "shadowban_reason": {
            "$ref": "#/components/schemas/PublisherShadowbanReason"
          },
          "shadowbanned_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "gatekept_follows": {
            "type": "boolean",
            "nullable": true
          },
          "moderate_subscription": {
            "type": "boolean",
            "nullable": true
          },
          "rating": {
            "type": "number",
            "format": "double"
          },
          "rating_level": {
            "type": "integer",
            "format": "int32",
            "readOnly": true
          },
          "is_shadowbanned": {
            "type": "boolean",
            "readOnly": true
          },
          "is_gatekept": {
            "type": "boolean",
            "readOnly": true
          },
          "is_moderate_subscription": {
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
      "SnPublisherFeature": {
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
          "flag": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "expired_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          }
        },
        "additionalProperties": false
      },
      "SnPublisherFollowRequest": {
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
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "state": {
            "$ref": "#/components/schemas/FollowRequestState"
          },
          "reviewed_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "reviewed_by_account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "reject_reason": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnPublisherMember": {
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
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          },
          "role": {
            "$ref": "#/components/schemas/PublisherMemberRole"
          },
          "joined_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "SnPublisherSubscription": {
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
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "last_read_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "notify": {
            "type": "boolean"
          },
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "end_reason": {
            "$ref": "#/components/schemas/SubscriptionEndReason"
          },
          "ended_by_account_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "is_active": {
            "type": "boolean",
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "SnPublisherVerifiedDomain": {
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
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "domain": {
            "maxLength": 512,
            "type": "string",
            "nullable": true
          },
          "status": {
            "$ref": "#/components/schemas/DomainVerificationStatus"
          },
          "verified_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "last_checked_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "failed_attempts": {
            "type": "integer",
            "format": "int32"
          },
          "last_error": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnPublishingSettings": {
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
          "default_posting_publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "default_reply_publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "default_fediverse_publisher_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          },
          "default_posting_publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "default_reply_publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "default_fediverse_publisher": {
            "$ref": "#/components/schemas/SnPublisher"
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
      "SnSticker": {
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
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "image": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
          },
          "size": {
            "$ref": "#/components/schemas/StickerSize"
          },
          "mode": {
            "$ref": "#/components/schemas/StickerMode"
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "pack_id": {
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
      "SnStickerBatchLookupItem": {
        "type": "object",
        "properties": {
          "placeholder": {
            "type": "string",
            "nullable": true
          },
          "sticker": {
            "$ref": "#/components/schemas/SnSticker"
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
      "SnSurvey": {
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
          "questions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnSurveyQuestion"
            },
            "nullable": true
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_anonymous": {
            "type": "boolean"
          },
          "status": {
            "$ref": "#/components/schemas/SurveyStatus"
          },
          "published_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "notify_subscribers": {
            "type": "boolean"
          },
          "attachments": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnCloudFileReferenceObject"
            },
            "nullable": true
          },
          "hide_results": {
            "type": "boolean"
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnSurveyAnswer": {
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
          "answer": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "survey_id": {
            "type": "string",
            "format": "uuid"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SnSurveyOption": {
        "required": [
          "label"
        ],
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "label": {
            "maxLength": 1024,
            "minLength": 1,
            "type": "string"
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "order": {
            "type": "integer",
            "format": "int32"
          }
        },
        "additionalProperties": false
      },
      "SnSurveyQuestion": {
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
            "$ref": "#/components/schemas/SurveyQuestionType"
          },
          "options": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnSurveyOption"
            },
            "nullable": true
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "is_required": {
            "type": "boolean"
          },
          "attachments": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnCloudFileReferenceObject"
            },
            "nullable": true
          },
          "max_selections": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "max_length": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "min_value": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "max_value": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "survey_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnSurveySubscription": {
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
          "survey_id": {
            "type": "string",
            "format": "uuid"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          }
        },
        "additionalProperties": false
      },
      "SnTimelineEvent": {
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
          "resource_identifier": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "meta": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          },
          "data": {
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SnTimelinePage": {
        "type": "object",
        "properties": {
          "items": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnTimelineEvent"
            },
            "nullable": true
          },
          "next_cursor": {
            "type": "string",
            "nullable": true
          },
          "mode": {
            "type": "string",
            "nullable": true
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
      "StartEgressRequest": {
        "type": "object",
        "properties": {
          "rtmp_urls": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "file_path": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "StartHlsEgressRequest": {
        "type": "object",
        "properties": {
          "playlist_name": {
            "type": "string",
            "nullable": true
          },
          "segment_duration": {
            "type": "integer",
            "format": "int32"
          },
          "segment_count": {
            "type": "integer",
            "format": "int32"
          },
          "layout": {
            "type": "string",
            "nullable": true
          },
          "hls_base_url": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "StartStreamingRequest": {
        "type": "object",
        "properties": {
          "participant_name": {
            "type": "string",
            "nullable": true
          },
          "no_ingress": {
            "type": "boolean",
            "nullable": true
          },
          "use_whip": {
            "type": "boolean",
            "nullable": true
          },
          "enable_transcoding": {
            "type": "boolean",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "StickerMode": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "StickerPack": {
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
          "icon": {
            "$ref": "#/components/schemas/SnCloudFileReferenceObject"
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
          "prefix": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "popularity": {
            "type": "integer",
            "format": "int32"
          },
          "stickers": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnSticker"
            },
            "nullable": true
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "publisher": {
            "$ref": "#/components/schemas/SnPublisher"
          },
          "resource_identifier": {
            "type": "string",
            "nullable": true,
            "readOnly": true
          }
        },
        "additionalProperties": false
      },
      "StickerPackOwnership": {
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
          "pack_id": {
            "type": "string",
            "format": "uuid"
          },
          "pack": {
            "$ref": "#/components/schemas/StickerPack"
          },
          "account_id": {
            "type": "string",
            "format": "uuid"
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "StickerPackRequest": {
        "type": "object",
        "properties": {
          "icon_id": {
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
          "prefix": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "StickerRequest": {
        "type": "object",
        "properties": {
          "slug": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "image_id": {
            "type": "string",
            "nullable": true
          },
          "size": {
            "$ref": "#/components/schemas/StickerSize"
          },
          "mode": {
            "$ref": "#/components/schemas/StickerMode"
          }
        },
        "additionalProperties": false
      },
      "StickerSize": {
        "enum": [
          0,
          1,
          2,
          3
        ],
        "type": "integer",
        "format": "int32"
      },
      "SubscribeRequest": {
        "type": "object",
        "additionalProperties": false
      },
      "SubscriberResponse": {
        "type": "object",
        "properties": {
          "subscription": {
            "$ref": "#/components/schemas/SnPublisherSubscription"
          },
          "account": {
            "$ref": "#/components/schemas/SnAccount"
          }
        },
        "additionalProperties": false
      },
      "SubscriptionEndReason": {
        "enum": [
          0,
          1
        ],
        "type": "integer",
        "format": "int32"
      },
      "SubscriptionReadStatus": {
        "type": "object",
        "properties": {
          "subscription": {
            "$ref": "#/components/schemas/SnPublisherSubscription"
          },
          "latest_content_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "has_new_content": {
            "type": "boolean"
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
      "SubscriptionStatusResponse": {
        "type": "object",
        "properties": {
          "subscription": {
            "$ref": "#/components/schemas/SnPublisherSubscription"
          },
          "follow_request": {
            "$ref": "#/components/schemas/SnPublisherFollowRequest"
          },
          "requires_approval": {
            "type": "boolean"
          },
          "status": {
            "type": "string",
            "nullable": true
          },
          "message": {
            "type": "string",
            "nullable": true
          },
          "is_pending": {
            "type": "boolean"
          },
          "is_active": {
            "type": "boolean"
          },
          "notify": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SubscriptionWithLiveStatus": {
        "type": "object",
        "properties": {
          "subscription": {
            "$ref": "#/components/schemas/SnPublisherSubscription"
          },
          "is_live": {
            "type": "boolean"
          },
          "latest_content_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "has_new_content": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "SurveyAnswerRequest": {
        "required": [
          "answer"
        ],
        "type": "object",
        "properties": {
          "answer": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SurveyQuestionType": {
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
      "SurveyRequest": {
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
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "clear_ended_at": {
            "type": "boolean",
            "nullable": true
          },
          "is_anonymous": {
            "type": "boolean",
            "nullable": true
          },
          "notify_subscribers": {
            "type": "boolean",
            "nullable": true
          },
          "hide_results": {
            "type": "boolean",
            "nullable": true
          },
          "attachments": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          },
          "questions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SurveyRequestQuestion"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SurveyRequestQuestion": {
        "type": "object",
        "properties": {
          "id": {
            "type": "string",
            "format": "uuid"
          },
          "type": {
            "$ref": "#/components/schemas/SurveyQuestionType"
          },
          "options": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnSurveyOption"
            },
            "nullable": true
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "order": {
            "type": "integer",
            "format": "int32"
          },
          "is_required": {
            "type": "boolean"
          },
          "max_selections": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "max_length": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "min_value": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "max_value": {
            "type": "number",
            "format": "double",
            "nullable": true
          },
          "attachments": {
            "type": "array",
            "items": {
              "type": "string"
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "SurveyStatus": {
        "enum": [
          0,
          1,
          2
        ],
        "type": "integer",
        "format": "int32"
      },
      "SurveyWithStats": {
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
          "questions": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnSurveyQuestion"
            },
            "nullable": true
          },
          "title": {
            "maxLength": 1024,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          },
          "ended_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "is_anonymous": {
            "type": "boolean"
          },
          "status": {
            "$ref": "#/components/schemas/SurveyStatus"
          },
          "published_at": {
            "$ref": "#/components/schemas/Instant"
          },
          "notify_subscribers": {
            "type": "boolean"
          },
          "attachments": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/SnCloudFileReferenceObject"
            },
            "nullable": true
          },
          "hide_results": {
            "type": "boolean"
          },
          "publisher_id": {
            "type": "string",
            "format": "uuid"
          },
          "user_answer": {
            "$ref": "#/components/schemas/SnSurveyAnswer"
          },
          "stats": {
            "type": "object",
            "additionalProperties": {
              "type": "object",
              "additionalProperties": {
                "type": "integer",
                "format": "int32"
              }
            },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "TestAutomodRequest": {
        "type": "object",
        "properties": {
          "content": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ThreadedReplyNode": {
        "required": [
          "depth",
          "parent_id",
          "post"
        ],
        "type": "object",
        "properties": {
          "post": {
            "$ref": "#/components/schemas/SnPost"
          },
          "depth": {
            "type": "integer",
            "format": "int32"
          },
          "parent_id": {
            "type": "string",
            "format": "uuid",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "TimeoutRequest": {
        "type": "object",
        "properties": {
          "duration_minutes": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "ToggleRuleRequest": {
        "type": "object",
        "properties": {
          "enabled": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "UpdateAutomodRuleRequest": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/AutomodRuleType"
          },
          "default_action": {
            "$ref": "#/components/schemas/AutomodRuleAction"
          },
          "pattern": {
            "type": "string",
            "nullable": true
          },
          "is_regex": {
            "type": "boolean",
            "nullable": true
          },
          "derank_weight": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "is_enabled": {
            "type": "boolean",
            "nullable": true
          },
          "priority": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateCategoryRequest": {
        "type": "object",
        "properties": {
          "slug": {
            "maxLength": 128,
            "type": "string",
            "nullable": true
          },
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateCollectionRequest": {
        "type": "object",
        "properties": {
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
          "background_id": {
            "type": "string",
            "nullable": true
          },
          "icon_id": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateFediverseModerationRuleRequest": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "nullable": true
          },
          "description": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/FediverseModerationRuleType"
          },
          "action": {
            "$ref": "#/components/schemas/FediverseModerationAction"
          },
          "domain": {
            "type": "string",
            "nullable": true
          },
          "keyword_pattern": {
            "type": "string",
            "nullable": true
          },
          "is_regex": {
            "type": "boolean",
            "nullable": true
          },
          "is_enabled": {
            "type": "boolean",
            "nullable": true
          },
          "report_threshold": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          },
          "priority": {
            "type": "integer",
            "format": "int32",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateLiveStreamRequest": {
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
          "slug": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "$ref": "#/components/schemas/LiveStreamType"
          },
          "visibility": {
            "$ref": "#/components/schemas/LiveStreamVisibility"
          },
          "metadata": {
            "type": "object",
            "additionalProperties": { },
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateNotifyRequest": {
        "type": "object",
        "properties": {
          "notify": {
            "type": "boolean"
          }
        },
        "additionalProperties": false
      },
      "UpdateSubscriptionReadStateRequest": {
        "type": "object",
        "properties": {
          "last_read_at": {
            "$ref": "#/components/schemas/Instant"
          }
        },
        "additionalProperties": false
      },
      "UpdateTagRequest": {
        "type": "object",
        "properties": {
          "name": {
            "maxLength": 256,
            "type": "string",
            "nullable": true
          },
          "description": {
            "maxLength": 4096,
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UpdateThumbnailRequest": {
        "type": "object",
        "properties": {
          "thumbnail_id": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "UserReactionListingItem": {
        "required": [
          "post",
          "reaction"
        ],
        "type": "object",
        "properties": {
          "reaction": {
            "$ref": "#/components/schemas/SnPostReaction"
          },
          "post": {
            "$ref": "#/components/schemas/SnPost"
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
      "WebFingerLink": {
        "type": "object",
        "properties": {
          "rel": {
            "type": "string",
            "nullable": true
          },
          "type": {
            "type": "string",
            "nullable": true
          },
          "href": {
            "type": "string",
            "nullable": true
          }
        },
        "additionalProperties": false
      },
      "WebFingerResponse": {
        "type": "object",
        "properties": {
          "subject": {
            "type": "string",
            "nullable": true
          },
          "links": {
            "type": "array",
            "items": {
              "$ref": "#/components/schemas/WebFingerLink"
            },
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
      "name": "Activity"
    },
    {
      "name": "ActivityPub"
    },
    {
      "name": "ActivityPubFollow"
    },
    {
      "name": "ActivityPubRealm"
    },
    {
      "name": "Ads"
    },
    {
      "name": "Autocompletion"
    },
    {
      "name": "Automod"
    },
    {
      "name": "FediverseActor"
    },
    {
      "name": "FediverseKey"
    },
    {
      "name": "FediverseKeyAdmin"
    },
    {
      "name": "FediverseModeration"
    },
    {
      "name": "LiveStream"
    },
    {
      "name": "NodeInfo"
    },
    {
      "name": "Post"
    },
    {
      "name": "PostAction"
    },
    {
      "name": "PostAdmin"
    },
    {
      "name": "PostCategory"
    },
    {
      "name": "PostTag"
    },
    {
      "name": "PostCategoryAdmin"
    },
    {
      "name": "PostCategorySub"
    },
    {
      "name": "PostCollection"
    },
    {
      "name": "PostCollectionAdmin"
    },
    {
      "name": "PostStatsAdmin"
    },
    {
      "name": "PostSubscription"
    },
    {
      "name": "PostTagAdmin"
    },
    {
      "name": "Publisher"
    },
    {
      "name": "PublisherPublic"
    },
    {
      "name": "PublisherAdmin"
    },
    {
      "name": "PublisherSubscription"
    },
    {
      "name": "PublishingSettings"
    },
    {
      "name": "QuoteAuthorization"
    },
    {
      "name": "ServerActor"
    },
    {
      "name": "Sticker"
    },
    {
      "name": "Survey"
    },
    {
      "name": "TimelineDiscovery"
    },
    {
      "name": "Translation"
    },
    {
      "name": "WebFinger"
    },
    {
      "name": "WebReader"
    }
  ]
}