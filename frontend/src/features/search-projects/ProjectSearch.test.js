import { mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import ProjectSearch from './ProjectSearch.vue'

it('does not search until the query contains two characters', async () => {
  const search = vi.fn().mockResolvedValue([])
  const wrapper = mount(ProjectSearch, { props: { search } })
  await wrapper.get('input').setValue('a')
  expect(search).not.toHaveBeenCalled()
  await wrapper.get('input').setValue('ab')
  expect(search).toHaveBeenCalledWith('ab')
})

it('returns to idle when the search query is empty', async () => {
  const wrapper = mount(ProjectSearch, { props: { search: vi.fn().mockResolvedValue([]) } })
  await wrapper.get('input').setValue('')
  expect(wrapper.find('.project-search__state').exists()).toBe(false)
})
