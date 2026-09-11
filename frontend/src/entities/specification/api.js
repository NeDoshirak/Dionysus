import { apiRequest } from '@/shared/api/client'
import { normalizeSpecification, toApiSpecificationItemBody } from './model'

const specificationPath = (projectId) => `/api/projects/${encodeURIComponent(projectId)}/specification`
const functionPath = (projectId, functionId) => `${specificationPath(projectId)}/functions/${encodeURIComponent(functionId)}`
const itemPath = (projectId, functionId, itemId) => `${functionPath(projectId, functionId)}/items/${encodeURIComponent(itemId)}`

export const getSpecification = (projectId) => apiRequest(specificationPath(projectId), { authenticated: true })
  .then(normalizeSpecification)
export const retrySpecification = (projectId) => apiRequest(`${specificationPath(projectId)}/retry`, { method: 'POST', authenticated: true })
export const createSpecificationFunction = (projectId, body) => apiRequest(`${specificationPath(projectId)}/functions`, { method: 'POST', body, authenticated: true })
export const updateSpecificationFunction = (projectId, functionId, body) => apiRequest(functionPath(projectId, functionId), { method: 'PATCH', body, authenticated: true })
export const deleteSpecificationFunction = (projectId, functionId) => apiRequest(functionPath(projectId, functionId), { method: 'DELETE', authenticated: true })
export const createSpecificationItem = (projectId, functionId, body) => apiRequest(`${functionPath(projectId, functionId)}/items`, { method: 'POST', body: toApiSpecificationItemBody(body), authenticated: true })
export const updateSpecificationItem = (projectId, functionId, itemId, body) => apiRequest(itemPath(projectId, functionId, itemId), { method: 'PATCH', body: toApiSpecificationItemBody(body), authenticated: true })
export const deleteSpecificationItem = (projectId, functionId, itemId) => apiRequest(itemPath(projectId, functionId, itemId), { method: 'DELETE', authenticated: true })
